using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Configuration;
using ReqNest.Core.Content;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Core.Notifications;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class RequesterPortalEndpoints
{
    public static IEndpointRouteBuilder MapRequesterPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var administration = endpoints.MapGroup("/api/portal")
            .RequireAuthorization()
            .WithTags("Requester portal");
        administration.MapGet("/settings", GetSettingsAsync);
        administration.MapPut("/settings", UpdateSettingsAsync);
        administration.MapPut("/projects/{projectId:guid}", UpdateProjectAsync);

        var portal = endpoints.MapGroup("/api/public/portal")
            .WithTags("Requester portal");
        portal.MapGet("/{tenantId:guid}", GetPortalAsync);
        portal.MapPost("/{tenantId:guid}/tickets", SubmitTicketAsync)
            .RequireRateLimiting("authentication");
        portal.MapGet("/tickets/{ticketId:guid}", GetTicketAsync);
        portal.MapPost("/tickets/{ticketId:guid}/comments", AddCommentAsync)
            .RequireRateLimiting("authentication");
        return endpoints;
    }

    private static async Task<IResult> GetSettingsAsync(
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.Forbidden(context);
        }

        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        var projects = await dbContext.Projects.AsNoTracking()
            .OrderBy(entity => entity.Key)
            .Select(entity => new PortalProjectResponse(
                entity.Id,
                entity.Key,
                entity.Name,
                entity.RequesterPortalEnabled))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new PortalSettingsResponse(
            tenant.Id,
            tenant.RequesterPortalEnabled,
            tenant.RequesterPortalIntroduction,
            projects));
    }

    private static async Task<IResult> UpdateSettingsAsync(
        UpdatePortalSettingsRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.Forbidden(context);
        }

        if (request.Introduction?.Length > 4000)
        {
            return ApiProblems.Validation(context, "Portal introduction text is too long.");
        }

        var tenant = await dbContext.Tenants.SingleAsync(cancellationToken);
        tenant.RequesterPortalEnabled = request.IsEnabled;
        tenant.RequesterPortalIntroduction = CleanOptional(request.Introduction);
        AddAudit(dbContext, context, tenant.Id, "portal.settings.updated", tenant.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid projectId,
        UpdatePortalProjectRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.Forbidden(context);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ApiProblems.NotFound(context, "Project");
        }

        project.RequesterPortalEnabled = request.IsEnabled;
        AddAudit(dbContext, context, authorization.TenantId, "portal.project.updated", project.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetPortalAsync(
        Guid tenantId,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == tenantId && entity.RequesterPortalEnabled, cancellationToken);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        var projects = await dbContext.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(entity => entity.TenantId == tenantId && entity.RequesterPortalEnabled && !entity.IsArchived)
            .OrderBy(entity => entity.Key)
            .Select(entity => new PortalProjectResponse(
                entity.Id,
                entity.Key,
                entity.Name,
                entity.RequesterPortalEnabled))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new PublicPortalResponse(
            tenant.Id,
            tenant.Name,
            tenant.ShortName,
            tenant.PrimaryColor,
            tenant.DefaultLanguage,
            tenant.RequesterPortalIntroduction,
            projects));
    }

    private static async Task<IResult> SubmitTicketAsync(
        Guid tenantId,
        SubmitRequesterTicketRequest request,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer sanitizer,
        ISlaCalculator slaCalculator,
        INotificationService notificationService,
        IWebhookEventPublisher webhookPublisher,
        CancellationToken cancellationToken)
    {
        var tenantExists = await dbContext.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(entity => entity.Id == tenantId && entity.RequesterPortalEnabled, cancellationToken);
        var projectExists = await dbContext.Projects.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(entity => entity.Id == request.ProjectId && entity.TenantId == tenantId &&
                                entity.RequesterPortalEnabled && !entity.IsArchived, cancellationToken);
        if (!tenantExists || !projectExists)
        {
            return Results.NotFound();
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var content = sanitizer.Sanitize(request.Description);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 160 ||
            !System.Net.Mail.MailAddress.TryCreate(request.Email, out _) ||
            string.IsNullOrWhiteSpace(content.PlainText))
        {
            return ApiProblems.Validation(context, "A valid name, email, title, and description are required.");
        }

        tenantContext.TenantId = tenantId;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var project = await dbContext.Projects
            .FromSqlInterpolated($"SELECT * FROM projects WHERE id = {request.ProjectId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var initialStatus = await dbContext.WorkflowStatuses.SingleAsync(
            entity => entity.WorkflowId == project.WorkflowId && entity.IsInitial,
            cancellationToken);
        var typeKey = string.IsNullOrWhiteSpace(request.TypeKey) ? "ServiceRequest" : request.TypeKey.Trim();
        var priorityKey = string.IsNullOrWhiteSpace(request.PriorityKey) ? "Normal" : request.PriorityKey.Trim();
        var schemaValid = await dbContext.TicketTypeDefinitions.AnyAsync(
                              entity => entity.IsActive && entity.Key == typeKey &&
                                        (entity.ProjectId == null || entity.ProjectId == project.Id), cancellationToken) &&
                          await dbContext.TicketPriorityDefinitions.AnyAsync(
                              entity => entity.IsActive && entity.Key == priorityKey &&
                                        (entity.ProjectId == null || entity.ProjectId == project.Id), cancellationToken);
        if (!schemaValid)
        {
            return ApiProblems.Validation(context, "The selected ticket type or priority is unavailable.");
        }

        var requester = await dbContext.RequesterIdentities.SingleOrDefaultAsync(
            entity => entity.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (requester is null)
        {
            requester = new RequesterIdentity
            {
                TenantId = tenantId,
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                DisplayName = request.DisplayName.Trim(),
                PreferredLanguage = request.Language,
            };
            dbContext.RequesterIdentities.Add(requester);
        }
        else
        {
            requester.DisplayName = request.DisplayName.Trim();
            requester.PreferredLanguage = request.Language;
        }

        var now = DateTimeOffset.UtcNow;
        var sla = await slaCalculator.CalculateAsync(project.Id, priorityKey, now, cancellationToken);
        var ticket = new Ticket
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Number = project.NextTicketNumber,
            Key = $"{project.Key}-{project.NextTicketNumber}",
            Title = request.Title.Trim(),
            Description = content.Html,
            DescriptionPlainText = content.PlainText,
            Type = Enum.TryParse<TicketType>(typeKey, out var legacyType) ? legacyType : TicketType.ServiceRequest,
            TypeKey = typeKey,
            Priority = Enum.TryParse<TicketPriority>(priorityKey, out var legacyPriority) ? legacyPriority : TicketPriority.Normal,
            PriorityKey = priorityKey,
            WorkflowStatusId = initialStatus.Id,
            RequesterIdentityId = requester.Id,
            ReporterEmailSnapshot = requester.Email,
            ReporterDisplayNameSnapshot = requester.DisplayName,
            AssigneeUserId = project.DefaultAssigneeUserId,
            FirstResponseTargetAt = sla?.FirstResponseTargetAt,
            ResolutionTargetAt = sla?.ResolutionTargetAt,
            SlaPolicyId = sla?.PolicyId,
            SlaPolicyNameSnapshot = sla?.PolicyName,
            SlaWarningAt = sla?.WarningAt,
            SlaState = sla is null ? SlaState.None : SlaState.OnTrack,
        };
        project.NextTicketNumber++;
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            Ticket = ticket,
            ToStatusId = initialStatus.Id,
        });
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        dbContext.RequesterTicketAccesses.Add(new RequesterTicketAccess
        {
            TenantId = tenantId,
            RequesterIdentityId = requester.Id,
            TicketId = ticket.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = now.AddDays(365),
        });
        dbContext.Tickets.Add(ticket);
        var audit = new AuditEvent
        {
            TenantId = tenantId,
            Action = "requester.ticket.created",
            TargetType = nameof(Ticket),
            TargetId = ticket.Id.ToString(),
            Summary = "A requester submitted a portal ticket.",
            CorrelationId = context.TraceIdentifier,
        };
        dbContext.AuditEvents.Add(audit);
        await webhookPublisher.PublishAsync(
            tenantId,
            "ticket.created",
            audit.Id.ToString(),
            new
            {
                ticket.Id,
                ticket.Key,
                ticket.ProjectId,
                ticket.Title,
                Source = "requester-portal",
            }, cancellationToken);
        var recipients = project.DefaultAssigneeUserId is null ? Array.Empty<Guid>() : [project.DefaultAssigneeUserId.Value];
        await notificationService.AddAsync(new NotificationMessage(
            tenantId,
            recipients,
            null,
            NotificationType.TicketAssigned,
            project.Id,
            ticket.Id,
            audit.Id.ToString(),
            $"{ticket.Key} was submitted through the requester portal.",
            $"/app/tickets/{ticket.Id}",
            ticket.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/public/portal/tickets/{ticket.Id}",
            new RequesterTicketCreatedResponse(ticket.Id, ticket.Key, rawToken));
    }

    private static async Task<IResult> GetTicketAsync(
        Guid ticketId,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(ticketId, context, tenantContext, dbContext, cancellationToken);
        if (access is null)
        {
            return Results.NotFound();
        }

        var ticket = await dbContext.Tickets.AsNoTracking()
            .Where(entity => entity.Id == ticketId)
            .Select(entity => new RequesterTicketResponse(
                entity.Id,
                entity.Key,
                entity.Title,
                entity.Description,
                entity.Project.Name,
                entity.WorkflowStatus.Label,
                entity.SlaState,
                entity.CreatedAt,
                entity.UpdatedAt))
            .SingleAsync(cancellationToken);
        var agentComments = await dbContext.TicketComments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId && !entity.IsHidden && !entity.IsDeleted)
            .Select(entity => new RequesterCommentResponse(entity.Id, entity.AuthorUser.DisplayName, entity.Body, false, entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        var requesterComments = await dbContext.RequesterComments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId && !entity.IsHidden)
            .Select(entity => new RequesterCommentResponse(
                entity.Id,
                access.RequesterName,
                entity.Body,
                true,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new RequesterTicketWithCommentsResponse(
            ticket,
            agentComments.Concat(requesterComments).OrderBy(item => item.CreatedAt).ToArray()));
    }

    private static async Task<IResult> AddCommentAsync(
        Guid ticketId,
        AddRequesterCommentRequest request,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer sanitizer,
        INotificationService notificationService,
        IWebhookEventPublisher webhookPublisher,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(ticketId, context, tenantContext, dbContext, cancellationToken);
        if (access is null)
        {
            return Results.NotFound();
        }

        var content = sanitizer.Sanitize(request.Body);
        if (string.IsNullOrWhiteSpace(content.PlainText))
        {
            return ApiProblems.Validation(context, "A comment is required.");
        }

        var comment = new RequesterComment
        {
            TenantId = access.TenantId,
            TicketId = ticketId,
            RequesterIdentityId = access.RequesterId,
            Body = content.Html,
            BodyPlainText = content.PlainText,
        };
        dbContext.RequesterComments.Add(comment);
        var ticket = await dbContext.Tickets.SingleAsync(entity => entity.Id == ticketId, cancellationToken);
        var recipientIds = await dbContext.TicketWatchers
            .Where(entity => entity.TicketId == ticketId && !entity.IsMuted)
            .Select(entity => entity.UserId)
            .Concat(dbContext.Tickets.Where(entity => entity.Id == ticketId && entity.AssigneeUserId != null)
                .Select(entity => entity.AssigneeUserId!.Value))
            .Distinct()
            .ToArrayAsync(cancellationToken);
        await notificationService.AddAsync(new NotificationMessage(
            access.TenantId,
            recipientIds,
            null,
            NotificationType.TicketCommented,
            ticket.ProjectId,
            ticket.Id,
            comment.Id.ToString(),
            $"{access.RequesterName} commented on {ticket.Key}.",
            $"/app/tickets/{ticket.Id}",
            ticket.Id.ToString()), cancellationToken);
        var audit = new AuditEvent
        {
            TenantId = access.TenantId,
            Action = "requester.ticket.commented",
            TargetType = nameof(Ticket),
            TargetId = ticket.Id.ToString(),
            Summary = "A requester added a portal comment.",
            CorrelationId = context.TraceIdentifier,
        };
        dbContext.AuditEvents.Add(audit);
        await webhookPublisher.PublishAsync(
            access.TenantId,
            "ticket.commented",
            audit.Id.ToString(),
            new
            {
                TicketId = ticket.Id,
                ticket.Key,
                CommentId = comment.Id,
                Source = "requester-portal",
            }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/public/portal/tickets/{ticketId}/comments/{comment.Id}",
            new RequesterCommentResponse(comment.Id, access.RequesterName, comment.Body, true, comment.CreatedAt));
    }

    private static async Task<PortalAccess?> AuthorizeAsync(
        Guid ticketId,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rawToken = context.Request.Headers["X-Requester-Token"].ToString();
        if (rawToken.Length != 64)
        {
            return null;
        }

        var tokenHash = Hash(rawToken);
        var access = await dbContext.RequesterTicketAccesses.IgnoreQueryFilters()
            .Where(entity => entity.TicketId == ticketId && entity.TokenHash == tokenHash &&
                             entity.RevokedAt == null && entity.ExpiresAt > DateTimeOffset.UtcNow)
            .Join(
                dbContext.RequesterIdentities.IgnoreQueryFilters(),
                item => item.RequesterIdentityId,
                requester => requester.Id,
                (item, requester) => new PortalAccess(item.Id, item.TenantId, requester.Id, requester.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null)
        {
            return null;
        }

        tenantContext.TenantId = access.TenantId;
        await dbContext.RequesterTicketAccesses.Where(entity => entity.Id == access.AccessId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(entity => entity.LastAccessedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        return access;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Guid tenantId,
        string action,
        Guid targetId) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = "RequesterPortal",
            TargetId = targetId.ToString(),
            Summary = "Requester portal configuration changed.",
            CorrelationId = context.TraceIdentifier,
        });

    private sealed record PortalAccess(Guid AccessId, Guid TenantId, Guid RequesterId, string RequesterName);
}

public sealed record UpdatePortalSettingsRequest(
    bool IsEnabled,
    string? Introduction);

public sealed record UpdatePortalProjectRequest(bool IsEnabled);

public sealed record PortalProjectResponse(
    Guid Id,
    string Key,
    string Name,
    bool IsEnabled);

public sealed record PortalSettingsResponse(
    Guid TenantId,
    bool IsEnabled,
    string? Introduction,
    IReadOnlyCollection<PortalProjectResponse> Projects);

public sealed record PublicPortalResponse(
    Guid TenantId,
    string CompanyName,
    string CompanyShortName,
    string PrimaryColor,
    AppLanguage DefaultLanguage,
    string? Introduction,
    IReadOnlyCollection<PortalProjectResponse> Projects);

public sealed record SubmitRequesterTicketRequest(
    Guid ProjectId,
    string DisplayName,
    string Email,
    AppLanguage Language,
    string Title,
    string Description,
    string? TypeKey,
    string? PriorityKey);

public sealed record RequesterTicketCreatedResponse(Guid TicketId, string Key, string AccessToken);

public sealed record RequesterTicketResponse(
    Guid Id,
    string Key,
    string Title,
    string Description,
    string ProjectName,
    string Status,
    SlaState SlaState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RequesterCommentResponse(
    Guid Id,
    string AuthorName,
    string Body,
    bool IsRequester,
    DateTimeOffset CreatedAt);

public sealed record RequesterTicketWithCommentsResponse(
    RequesterTicketResponse Ticket,
    IReadOnlyCollection<RequesterCommentResponse> Comments);

public sealed record AddRequesterCommentRequest(string Body);
