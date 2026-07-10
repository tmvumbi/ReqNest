using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class AiAssistanceEndpoints
{
    public static IEndpointRouteBuilder MapAiAssistanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tickets/{ticketId:guid}/ai-assistance").RequireAuthorization().WithTags("AI assistance");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{requestId:guid}/review", ReviewAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(Guid ticketId, HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var access = await TicketAccessAsync(ticketId, context, dbContext, cancellationToken);
        if (access is not null) return access;
        var items = await dbContext.AiAssistanceRequests.AsNoTracking().Where(item => item.TicketId == ticketId)
            .OrderByDescending(item => item.CreatedAt).Take(50).Select(item => ToResponse(item)).ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<AiAssistanceResponse>>(items);
    }

    private static async Task<IResult> CreateAsync(
        Guid ticketId, CreateAiAssistanceRequest request, HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var access = await TicketAccessAsync(ticketId, context, dbContext, cancellationToken);
        if (access is not null) return access;
        var configuration = await dbContext.AiTenantConfigurations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (configuration is null || !configuration.IsEnabled || configuration.NonTrainingAssuranceAcceptedAt is null ||
            !configuration.RequireHumanReview || !configuration.AllowedKinds.Contains(request.Kind))
            return ApiProblems.Conflict(context, "AI assistance is not enabled for this task.", "ai_not_enabled");
        var ticket = await dbContext.Tickets.AsNoTracking().SingleAsync(item => item.Id == ticketId, cancellationToken);
        var comments = await dbContext.TicketComments.AsNoTracking().Where(item => item.TicketId == ticketId && !item.IsHidden)
            .OrderByDescending(item => item.CreatedAt).Select(item => item.BodyPlainText).Take(5).ToArrayAsync(cancellationToken);
        // Only bounded text fields are used; attachment contents and requester contact details are excluded.
        var minimized = string.Join("\n", new[] { ticket.Title, ticket.DescriptionPlainText }.Concat(comments))
            .Replace("\0", "", StringComparison.Ordinal);
        minimized = minimized[..Math.Min(minimized.Length, 8_000)];
        var draft = request.Kind switch
        {
            AiAssistanceKind.Summarize => Summarize(minimized),
            AiAssistanceKind.SuggestReply => SuggestReply(ticket.Title, minimized),
            AiAssistanceKind.Classify => Classify(minimized),
            _ => throw new InvalidOperationException("Unsupported assistance kind."),
        };
        var entity = new AiAssistanceRequest
        {
            TenantId = context.TenantAuthorization()!.TenantId,
            TicketId = ticketId,
            RequestedByUserId = context.User.UserId(),
            Kind = request.Kind,
            InputFingerprint = Hash(minimized),
            DraftOutput = draft,
            Status = AiAssistanceStatus.Draft,
            EvaluationScore = Evaluate(draft),
        };
        if (entity.EvaluationScore < 0.8m)
        {
            entity.Status = AiAssistanceStatus.Failed;
            entity.FailureCode = "evaluation_failed";
        }
        dbContext.AiAssistanceRequests.Add(entity);
        dbContext.AuditEvents.Add(Audit(context, ticketId, "ai.assistance.created", "An AI-assisted draft was generated for human review."));
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/tickets/{ticketId}/ai-assistance/{entity.Id}", ToResponse(entity));
    }

    private static async Task<IResult> ReviewAsync(
        Guid ticketId, Guid requestId, ReviewAiAssistanceRequest request, HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var access = await TicketAccessAsync(ticketId, context, dbContext, cancellationToken);
        if (access is not null) return access;
        var entity = await dbContext.AiAssistanceRequests.SingleOrDefaultAsync(item => item.Id == requestId && item.TicketId == ticketId, cancellationToken);
        if (entity is null) return ApiProblems.NotFound(context, "AI assistance request");
        if (entity.Status != AiAssistanceStatus.Draft) return ApiProblems.Conflict(context, "This draft has already been reviewed.", "ai_already_reviewed");
        entity.Status = request.Accept ? AiAssistanceStatus.Accepted : AiAssistanceStatus.Rejected;
        entity.ReviewedByUserId = context.User.UserId();
        entity.ReviewedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(Audit(context, ticketId, request.Accept ? "ai.assistance.accepted" : "ai.assistance.rejected", "A human reviewed an AI-assisted draft."));
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static async Task<IResult?> TicketAccessAsync(Guid ticketId, HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization is null) return ApiProblems.TenantRequired(context);
        var projectId = await dbContext.Tickets.AsNoTracking().Where(item => item.Id == ticketId).Select(item => (Guid?)item.ProjectId).SingleOrDefaultAsync(cancellationToken);
        return projectId is null || !authorization.CanMaintainTickets(projectId.Value) ? ApiProblems.NotFound(context, "Ticket") : null;
    }

    private static string Summarize(string text)
    {
        var sentences = text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(3);
        return string.Join(". ", sentences)[..Math.Min(string.Join(". ", sentences).Length, 1_200)] + ".";
    }
    private static string SuggestReply(string title, string text) =>
        $"Thank you for contacting us about {title}. We have reviewed the details and are investigating the request. " +
        (text.Contains("urgent", StringComparison.OrdinalIgnoreCase) ? "We have noted the urgency. " : "") +
        "A contributor will confirm the next step. Please review and personalize this draft before sending.";
    private static string Classify(string text) => text.Contains("error", StringComparison.OrdinalIgnoreCase) || text.Contains("broken", StringComparison.OrdinalIgnoreCase)
        ? "{\"suggestedType\":\"Incident\",\"suggestedPriority\":\"High\",\"confidence\":0.82}"
        : "{\"suggestedType\":\"ServiceRequest\",\"suggestedPriority\":\"Normal\",\"confidence\":0.78}";
    private static decimal Evaluate(string output) => string.IsNullOrWhiteSpace(output) || output.Length > 5_000 ? 0m : 1m;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static AuditEvent Audit(HttpContext context, Guid ticketId, string action, string summary) => new()
    {
        TenantId = context.TenantAuthorization()!.TenantId,
        ActorUserId = context.User.UserId(),
        Action = action,
        TargetType = "Ticket",
        TargetId = ticketId.ToString(),
        Summary = summary,
        CorrelationId = context.TraceIdentifier,
    };
    private static AiAssistanceResponse ToResponse(AiAssistanceRequest item) => new(item.Id, item.Kind, item.DraftOutput, item.Status,
        item.EvaluationScore, item.RequestedByUserId, item.ReviewedByUserId, item.ReviewedAt, item.CreatedAt);
}

public sealed record CreateAiAssistanceRequest(AiAssistanceKind Kind);
public sealed record ReviewAiAssistanceRequest(bool Accept);
public sealed record AiAssistanceResponse(Guid Id, AiAssistanceKind Kind, string DraftOutput, AiAssistanceStatus Status,
    decimal EvaluationScore, Guid RequestedByUserId, Guid? ReviewedByUserId, DateTimeOffset? ReviewedAt, DateTimeOffset CreatedAt);
