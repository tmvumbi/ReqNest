using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Content;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static partial class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/knowledge")
            .RequireAuthorization()
            .WithTags("Knowledge base");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{articleId:guid}", UpdateAsync);
        group.MapPost("/{articleId:guid}/status", SetStatusAsync);
        group.MapPost("/{articleId:guid}/tickets/{ticketId:guid}", LinkTicketAsync);
        group.MapDelete("/{articleId:guid}/tickets/{ticketId:guid}", UnlinkTicketAsync);
        group.MapGet("/tickets/{ticketId:guid}", ListForTicketAsync);

        endpoints.MapGet("/api/public/portal/{tenantId:guid}/knowledge", PublicListAsync)
            .WithTags("Knowledge base");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        Guid? projectId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        if (projectId is not null && !authorization.CanAccessProject(projectId.Value))
        {
            return ApiProblems.NotFound(context, "Project");
        }

        var query = dbContext.KnowledgeArticles.AsNoTracking()
            .Where(entity => entity.ProjectId == null || projectId == null || entity.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(entity => EF.Functions.ILike(entity.SearchText, pattern));
        }

        var articles = await query.OrderByDescending(entity => entity.UpdatedAt)
            .Take(500)
            .Select(entity => ToResponse(entity))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<KnowledgeArticleResponse>>(articles);
    }

    private static async Task<IResult> CreateAsync(
        UpsertKnowledgeArticleRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        IRichContentSanitizer sanitizer,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        var error = ValidateAccess(request, authorization, context);
        if (error is not null)
        {
            return error;
        }

        var article = new KnowledgeArticle
        {
            TenantId = authorization!.TenantId,
            AuthorUserId = context.User.UserId(),
        };
        var validation = Apply(article, request, sanitizer, context);
        if (validation is not null)
        {
            return validation;
        }

        dbContext.KnowledgeArticles.Add(article);
        AddAudit(dbContext, context, authorization.TenantId, "knowledge.created", article.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/knowledge/{article.Id}", ToResponse(article));
    }

    private static async Task<IResult> UpdateAsync(
        Guid articleId,
        UpsertKnowledgeArticleRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        IRichContentSanitizer sanitizer,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        var error = ValidateAccess(request, authorization, context);
        if (error is not null)
        {
            return error;
        }

        var article = await dbContext.KnowledgeArticles.SingleOrDefaultAsync(entity => entity.Id == articleId, cancellationToken);
        if (article is null)
        {
            return ApiProblems.NotFound(context, "Knowledge article");
        }

        var validation = Apply(article, request, sanitizer, context);
        if (validation is not null)
        {
            return validation;
        }

        AddAudit(dbContext, context, authorization!.TenantId, "knowledge.updated", article.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(article));
    }

    private static async Task<IResult> SetStatusAsync(
        Guid articleId,
        SetKnowledgeStatusRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        var article = await dbContext.KnowledgeArticles.SingleOrDefaultAsync(entity => entity.Id == articleId, cancellationToken);
        if (article is null || article.ProjectId is not null && !authorization.CanManageProject(article.ProjectId.Value))
        {
            return ApiProblems.NotFound(context, "Knowledge article");
        }

        if (article.ProjectId is null && !authorization.IsTenantAdministrator())
        {
            return ApiProblems.Forbidden(context);
        }

        article.Status = request.Status;
        article.PublishedAt = request.Status == KnowledgeArticleStatus.Published
            ? article.PublishedAt ?? DateTimeOffset.UtcNow
            : null;
        AddAudit(dbContext, context, authorization.TenantId, "knowledge.status.updated", article.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(article));
    }

    private static async Task<IResult> LinkTicketAsync(
        Guid articleId,
        Guid ticketId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        var ticket = await dbContext.Tickets.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == ticketId, cancellationToken);
        var article = await dbContext.KnowledgeArticles.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == articleId, cancellationToken);
        if (authorization is null || ticket is null || article is null || !authorization.CanMaintainTickets(ticket.ProjectId))
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.NotFound(context, "Ticket or article");
        }

        if (!await dbContext.TicketKnowledgeArticles.AnyAsync(
                entity => entity.TicketId == ticketId && entity.KnowledgeArticleId == articleId,
                cancellationToken))
        {
            dbContext.TicketKnowledgeArticles.Add(new TicketKnowledgeArticle
            {
                TenantId = authorization.TenantId,
                TicketId = ticketId,
                KnowledgeArticleId = articleId,
                LinkedByUserId = context.User.UserId(),
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> UnlinkTicketAsync(
        Guid articleId,
        Guid ticketId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        var projectId = await dbContext.Tickets.AsNoTracking().Where(entity => entity.Id == ticketId)
            .Select(entity => (Guid?)entity.ProjectId).SingleOrDefaultAsync(cancellationToken);
        if (authorization is null || projectId is null || !authorization.CanMaintainTickets(projectId.Value))
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.NotFound(context, "Ticket");
        }

        await dbContext.TicketKnowledgeArticles
            .Where(entity => entity.TicketId == ticketId && entity.KnowledgeArticleId == articleId)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListForTicketAsync(
        Guid ticketId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        var projectId = await dbContext.Tickets.AsNoTracking().Where(entity => entity.Id == ticketId)
            .Select(entity => (Guid?)entity.ProjectId).SingleOrDefaultAsync(cancellationToken);
        if (authorization is null || projectId is null || !authorization.CanAccessProject(projectId.Value))
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.NotFound(context, "Ticket");
        }

        var articles = await dbContext.TicketKnowledgeArticles.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .Join(dbContext.KnowledgeArticles, link => link.KnowledgeArticleId, article => article.Id, (_, article) => article)
            .Select(entity => ToResponse(entity))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<KnowledgeArticleResponse>>(articles);
    }

    private static async Task<IResult> PublicListAsync(
        Guid tenantId,
        string? search,
        Guid? projectId,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Tenants.IgnoreQueryFilters().AnyAsync(
                entity => entity.Id == tenantId && entity.RequesterPortalEnabled,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var query = dbContext.KnowledgeArticles.IgnoreQueryFilters().AsNoTracking()
            .Where(entity => entity.TenantId == tenantId &&
                             entity.Status == KnowledgeArticleStatus.Published &&
                             entity.Visibility == KnowledgeArticleVisibility.Requesters &&
                             (entity.ProjectId == null || entity.ProjectId == projectId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(entity => EF.Functions.ILike(entity.SearchText, pattern));
        }

        var articles = await query.OrderBy(entity => entity.Title).Take(100)
            .Select(entity => ToResponse(entity)).ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<KnowledgeArticleResponse>>(articles);
    }

    private static IResult? ValidateAccess(
        UpsertKnowledgeArticleRequest request,
        TenantAuthorization? authorization,
        HttpContext context)
    {
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        return request.ProjectId is null
            ? authorization.IsTenantAdministrator() ? null : ApiProblems.Forbidden(context)
            : authorization.CanManageProject(request.ProjectId.Value) ? null : ApiProblems.Forbidden(context);
    }

    private static IResult? Apply(
        KnowledgeArticle article,
        UpsertKnowledgeArticleRequest request,
        IRichContentSanitizer sanitizer,
        HttpContext context)
    {
        var english = sanitizer.Sanitize(request.Body);
        var slug = SlugRegex().Replace(request.Slug.Trim().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length is < 2 or > 180 || string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(english.PlainText))
        {
            return ApiProblems.Validation(context, "A valid slug and a complete article are required.");
        }

        article.ProjectId = request.ProjectId;
        article.Slug = slug;
        article.Title = request.Title.Trim()[..Math.Min(300, request.Title.Trim().Length)];
        article.Body = english.Html;
        article.SearchText = $"{article.Title} {english.PlainText}"[..Math.Min(
            20_000,
            article.Title.Length + english.PlainText.Length + 1)];
        article.Visibility = request.Visibility;
        return null;
    }

    private static KnowledgeArticleResponse ToResponse(KnowledgeArticle entity) => new(
        entity.Id, entity.ProjectId, entity.Slug, entity.Title,
        entity.Body, entity.Status, entity.Visibility,
        entity.PublishedAt, entity.UpdatedAt);

    private static void AddAudit(ReqNestDbContext dbContext, HttpContext context, Guid tenantId, string action, Guid id) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = nameof(KnowledgeArticle),
            TargetId = id.ToString(),
            Summary = "Knowledge base content changed.",
            CorrelationId = context.TraceIdentifier,
        });

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}

public sealed record UpsertKnowledgeArticleRequest(
    Guid? ProjectId,
    string Slug,
    string Title,
    string Body,
    KnowledgeArticleVisibility Visibility);

public sealed record SetKnowledgeStatusRequest(KnowledgeArticleStatus Status);

public sealed record KnowledgeArticleResponse(
    Guid Id,
    Guid? ProjectId,
    string Slug,
    string Title,
    string Body,
    KnowledgeArticleStatus Status,
    KnowledgeArticleVisibility Visibility,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt);
