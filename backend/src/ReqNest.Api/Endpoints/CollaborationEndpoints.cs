using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Content;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Core.Notifications;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class CollaborationEndpoints
{
    public static IEndpointRouteBuilder MapCollaborationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tickets/{ticketId:guid}")
            .RequireAuthorization()
            .WithTags("Ticket collaboration");
        group.MapGet("/comments", ListCommentsAsync);
        group.MapPost("/comments", AddCommentAsync);
        group.MapPatch("/comments/{commentId:guid}", EditCommentAsync);
        group.MapDelete("/comments/{commentId:guid}", DeleteCommentAsync);
        group.MapPost("/comments/{commentId:guid}/hide", HideCommentAsync);
        group.MapPost("/watchers/me", WatchAsync);
        group.MapDelete("/watchers/me", UnwatchAsync);
        group.MapPatch("/watchers/me", SetMuteAsync);
        group.MapGet("/activity", ActivityAsync);
        return endpoints;
    }

    private static async Task<IResult> ListCommentsAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var comments = await dbContext.TicketComments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => new TicketCommentResponse(
                entity.Id,
                entity.AuthorUserId,
                entity.AuthorUser.DisplayName,
                entity.IsHidden || entity.IsDeleted ? string.Empty : entity.Body,
                entity.IsHidden,
                entity.IsDeleted,
                entity.EditedAt,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var requesterComments = await dbContext.RequesterComments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => new TicketCommentResponse(
                entity.Id,
                null,
                dbContext.RequesterIdentities
                    .Where(requester => requester.Id == entity.RequesterIdentityId)
                    .Select(requester => requester.DisplayName)
                    .Single(),
                entity.IsHidden ? string.Empty : entity.Body,
                entity.IsHidden,
                false,
                null,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<TicketCommentResponse>>(comments
            .Concat(requesterComments)
            .OrderBy(entity => entity.CreatedAt)
            .ToArray());
    }

    private static async Task<IResult> AddCommentAsync(
        Guid ticketId,
        CreateCommentRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer contentSanitizer,
        ITenantAuthorizationService authorizationService,
        INotificationService notificationService,
        IWebhookEventPublisher webhookPublisher,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken, tracked: true);
        if (access.Error is not null)
        {
            return access.Error;
        }

        if (access.Ticket!.IsArchived)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var content = contentSanitizer.Sanitize(request.Body);
        if (string.IsNullOrWhiteSpace(content.PlainText))
        {
            return ApiProblems.Validation(httpContext, "A comment is required.");
        }

        var mentionUserIds = (request.MentionUserIds ?? []).Concat(content.MentionUserIds).Distinct().ToArray();
        foreach (var userId in mentionUserIds)
        {
            if (!await TicketEndpoints.CanUseProjectAsync(
                    authorizationService,
                    userId,
                    access.Authorization!.TenantId,
                    access.Ticket.ProjectId,
                    cancellationToken))
            {
                return ApiProblems.Validation(httpContext, "A mentioned user cannot access this ticket.", "invalid_mention");
            }
        }

        var actorUserId = httpContext.User.UserId();
        var comment = new TicketComment
        {
            TenantId = access.Ticket.TenantId,
            TicketId = access.Ticket.Id,
            AuthorUserId = actorUserId,
            Body = content.Html,
            BodyPlainText = content.PlainText,
        };
        dbContext.TicketComments.Add(comment);
        if (access.Ticket.FirstRespondedAt is null && actorUserId != access.Ticket.ReporterUserId)
        {
            access.Ticket.FirstRespondedAt = DateTimeOffset.UtcNow;
        }

        var audit = TicketEndpoints.AddAudit(dbContext, httpContext, access.Ticket, "ticket.commented", "A comment was added.");
        await webhookPublisher.PublishAsync(access.Authorization!.TenantId, "ticket.commented", audit.Id.ToString(), new
        {
            ticketId = access.Ticket.Id,
            access.Ticket.Key,
            access.Ticket.ProjectId,
            commentId = comment.Id,
        }, cancellationToken);
        var regularRecipients = await TicketEndpoints.NotificationRecipientsAsync(dbContext, access.Ticket, cancellationToken);
        var mentionRecipients = mentionUserIds;
        await notificationService.AddAsync(new NotificationMessage(
            access.Ticket.TenantId,
            regularRecipients.Except(mentionRecipients).ToArray(),
            actorUserId,
            NotificationType.TicketCommented,
            access.Ticket.ProjectId,
            access.Ticket.Id,
            $"{audit.Id}:comment",
            $"A comment was added to {access.Ticket.Key}.",
            $"/app/tickets/{access.Ticket.Id}",
            access.Ticket.Id.ToString()), cancellationToken);
        await notificationService.AddAsync(new NotificationMessage(
            access.Ticket.TenantId,
            mentionRecipients,
            actorUserId,
            NotificationType.UserMentioned,
            access.Ticket.ProjectId,
            access.Ticket.Id,
            $"{audit.Id}:mention",
            $"You were mentioned on {access.Ticket.Key}.",
            $"/app/tickets/{access.Ticket.Id}",
            access.Ticket.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/tickets/{ticketId}/comments/{comment.Id}", ToResponse(comment, httpContext.User.Identity?.Name));
    }

    private static async Task<IResult> EditCommentAsync(
        Guid ticketId,
        Guid commentId,
        EditCommentRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer contentSanitizer,
        ITenantAuthorizationService authorizationService,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var comment = await dbContext.TicketComments.SingleOrDefaultAsync(
            entity => entity.Id == commentId && entity.TicketId == ticketId,
            cancellationToken);
        if (comment is null)
        {
            return ApiProblems.NotFound(httpContext, "Comment");
        }

        var userId = httpContext.User.UserId();
        var isManager = access.Authorization!.CanManageProject(access.Ticket!.ProjectId);
        if (comment.AuthorUserId != userId && !isManager || comment.IsDeleted || comment.IsHidden)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var content = contentSanitizer.Sanitize(request.Body);
        if (string.IsNullOrWhiteSpace(content.PlainText))
        {
            return ApiProblems.Validation(httpContext, "A comment is required.");
        }

        var previousMentions = contentSanitizer.Sanitize(comment.Body).MentionUserIds;
        var addedMentions = new List<Guid>();
        foreach (var mentionUserId in content.MentionUserIds.Except(previousMentions))
        {
            if (await TicketEndpoints.CanUseProjectAsync(
                    authorizationService,
                    mentionUserId,
                    access.Authorization!.TenantId,
                    access.Ticket.ProjectId,
                    cancellationToken))
            {
                addedMentions.Add(mentionUserId);
            }
        }

        comment.Revisions.Add(new TicketCommentRevision
        {
            TenantId = comment.TenantId,
            TicketCommentId = comment.Id,
            EditedByUserId = userId,
            PreviousBody = comment.Body,
        });
        comment.Body = content.Html;
        comment.BodyPlainText = content.PlainText;
        comment.EditedAt = DateTimeOffset.UtcNow;
        var audit = TicketEndpoints.AddAudit(dbContext, httpContext, access.Ticket, "ticket.comment.edited", "A comment was edited.");
        await notificationService.AddAsync(new NotificationMessage(
            access.Ticket.TenantId,
            addedMentions,
            userId,
            NotificationType.UserMentioned,
            access.Ticket.ProjectId,
            access.Ticket.Id,
            $"{audit.Id}:mention",
            $"You were mentioned on {access.Ticket.Key}.",
            $"/app/tickets/{access.Ticket.Id}",
            access.Ticket.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(comment, null));
    }

    private static async Task<IResult> DeleteCommentAsync(
        Guid ticketId,
        Guid commentId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var comment = await dbContext.TicketComments.SingleOrDefaultAsync(
            entity => entity.Id == commentId && entity.TicketId == ticketId,
            cancellationToken);
        if (comment is null)
        {
            return ApiProblems.NotFound(httpContext, "Comment");
        }

        var userId = httpContext.User.UserId();
        var isManager = access.Authorization!.CanManageProject(access.Ticket!.ProjectId);
        var isOwnRecentComment = comment.AuthorUserId == userId && comment.CreatedAt >= DateTimeOffset.UtcNow.AddMinutes(-15);
        if (!isManager && !isOwnRecentComment)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        comment.IsDeleted = true;
        comment.Body = string.Empty;
        comment.BodyPlainText = string.Empty;
        TicketEndpoints.AddAudit(dbContext, httpContext, access.Ticket, "ticket.comment.deleted", "A comment was deleted.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> HideCommentAsync(
        Guid ticketId,
        Guid commentId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        if (!access.Authorization!.CanManageProject(access.Ticket!.ProjectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var comment = await dbContext.TicketComments.SingleOrDefaultAsync(
            entity => entity.Id == commentId && entity.TicketId == ticketId,
            cancellationToken);
        if (comment is null)
        {
            return ApiProblems.NotFound(httpContext, "Comment");
        }

        comment.IsHidden = true;
        TicketEndpoints.AddAudit(dbContext, httpContext, access.Ticket, "ticket.comment.hidden", "A comment was hidden by a project manager.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static Task<IResult> WatchAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetWatchAsync(ticketId, true, false, httpContext, dbContext, cancellationToken);

    private static Task<IResult> UnwatchAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetWatchAsync(ticketId, false, false, httpContext, dbContext, cancellationToken);

    private static Task<IResult> SetMuteAsync(
        Guid ticketId,
        SetWatcherMuteRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetWatchAsync(ticketId, true, request.Muted, httpContext, dbContext, cancellationToken);

    private static async Task<IResult> SetWatchAsync(
        Guid ticketId,
        bool watching,
        bool muted,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken, tracked: true);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var userId = httpContext.User.UserId();
        var watcher = await dbContext.TicketWatchers.SingleOrDefaultAsync(
            entity => entity.TicketId == ticketId && entity.UserId == userId,
            cancellationToken);
        if (!watching && watcher is not null)
        {
            dbContext.TicketWatchers.Remove(watcher);
        }
        else if (watching && watcher is null)
        {
            dbContext.TicketWatchers.Add(new TicketWatcher
            {
                TenantId = access.Ticket!.TenantId,
                TicketId = ticketId,
                UserId = userId,
                IsMuted = muted,
            });
        }
        else if (watcher is not null)
        {
            watcher.IsMuted = muted;
        }

        TicketEndpoints.AddAudit(
            dbContext,
            httpContext,
            access.Ticket!,
            watching ? "ticket.watcher.updated" : "ticket.watcher.removed",
            watching ? "Watcher settings were updated." : "A watcher was removed.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ActivityAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await GetTicketAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var targetId = ticketId.ToString();
        var audits = await dbContext.AuditEvents.AsNoTracking()
            .Where(entity => entity.TargetType == nameof(Ticket) && entity.TargetId == targetId)
            .Select(entity => new TicketActivityResponse(entity.Id, "change", entity.Action, entity.Summary, entity.ActorUserId, entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        var comments = await dbContext.TicketComments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .Select(entity => new TicketActivityResponse(
                entity.Id,
                "comment",
                entity.IsDeleted ? "ticket.comment.deleted" : entity.IsHidden ? "ticket.comment.hidden" : "ticket.commented",
                entity.IsDeleted || entity.IsHidden ? string.Empty : entity.Body,
                entity.AuthorUserId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        var transitions = await dbContext.TicketStatusHistory.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .Select(entity => new TicketActivityResponse(
                entity.Id,
                "transition",
                "ticket.transitioned",
                entity.Comment ?? string.Empty,
                entity.ChangedByUserId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        var requesterComments = await dbContext.RequesterComments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .Select(entity => new TicketActivityResponse(
                entity.Id,
                "comment",
                "requester.ticket.commented",
                entity.IsHidden ? string.Empty : entity.Body,
                null,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        var attachments = await dbContext.Attachments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId)
            .Select(entity => new TicketActivityResponse(
                entity.Id,
                "attachment",
                entity.DeletedAt == null ? "attachment.uploaded" : "attachment.deleted",
                entity.OriginalFileName,
                entity.UploadedByUserId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<TicketActivityResponse>>(audits
            .Concat(comments)
            .Concat(requesterComments)
            .Concat(transitions)
            .Concat(attachments)
            .OrderByDescending(entity => entity.OccurredAt)
            .Take(200)
            .ToArray());
    }

    private static async Task<TicketAccess> GetTicketAccessAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return new TicketAccess(null, null, ApiProblems.TenantRequired(httpContext));
        }

        var query = dbContext.Tickets.AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        var ticket = await query.SingleOrDefaultAsync(entity => entity.Id == ticketId, cancellationToken);
        if (ticket is null || !authorization.CanAccessProject(ticket.ProjectId))
        {
            return new TicketAccess(null, authorization, ApiProblems.NotFound(httpContext, "Ticket"));
        }

        return new TicketAccess(ticket, authorization, null);
    }

    private static TicketCommentResponse ToResponse(TicketComment entity, string? displayName) => new(
        entity.Id,
        entity.AuthorUserId,
        displayName ?? string.Empty,
        entity.Body,
        entity.IsHidden,
        entity.IsDeleted,
        entity.EditedAt,
        entity.CreatedAt,
        entity.UpdatedAt);

    private sealed record TicketAccess(Ticket? Ticket, TenantAuthorization? Authorization, IResult? Error);
}

public sealed record CreateCommentRequest(string Body, IReadOnlyCollection<Guid>? MentionUserIds = null);

public sealed record EditCommentRequest(string Body);

public sealed record SetWatcherMuteRequest(bool Muted);

public sealed record TicketCommentResponse(
    Guid Id,
    Guid? AuthorUserId,
    string AuthorDisplayName,
    string Body,
    bool IsHidden,
    bool IsDeleted,
    DateTimeOffset? EditedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TicketActivityResponse(
    Guid Id,
    string Category,
    string Action,
    string Summary,
    Guid? ActorUserId,
    DateTimeOffset OccurredAt);
