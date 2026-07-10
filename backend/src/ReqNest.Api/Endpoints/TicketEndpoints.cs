using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Content;
using ReqNest.Core.Identity;
using ReqNest.Core.Notifications;
using ReqNest.Core.Tickets;
using ReqNest.Core.Workflows;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tickets")
            .RequireAuthorization()
            .WithTags("Tickets");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{ticketId:guid}", GetAsync);
        group.MapPatch("/{ticketId:guid}", UpdateAsync);
        group.MapPost("/{ticketId:guid}/transition", TransitionAsync);
        group.MapPost("/{ticketId:guid}/archive", ArchiveAsync);
        group.MapPost("/{ticketId:guid}/restore", RestoreAsync);
        group.MapPost("/bulk", BulkUpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid? projectId,
        Guid? statusId,
        TicketPriority? priority,
        TicketType? type,
        Guid? assigneeUserId,
        string? search,
        string? queue,
        bool? includeArchived,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var requestedPage = Math.Max(1, page ?? 1);
        var requestedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var accessibleProjectIds = authorization.ProjectRoles.Keys.ToArray();
        var allProjects = authorization.AllProjectRoles.Count > 0;
        var userId = httpContext.User.UserId();
        var queryable = dbContext.Tickets
            .AsNoTracking()
            .Where(entity => allProjects || accessibleProjectIds.Contains(entity.ProjectId))
            .Where(entity => includeArchived == true || !entity.IsArchived);

        if (projectId is not null)
        {
            if (!authorization.CanAccessProject(projectId.Value))
            {
                return ApiProblems.NotFound(httpContext, "Project");
            }

            queryable = queryable.Where(entity => entity.ProjectId == projectId);
        }

        if (statusId is not null)
        {
            queryable = queryable.Where(entity => entity.WorkflowStatusId == statusId);
        }

        if (priority is not null)
        {
            queryable = queryable.Where(entity => entity.Priority == priority);
        }

        if (type is not null)
        {
            queryable = queryable.Where(entity => entity.Type == type);
        }

        if (assigneeUserId is not null)
        {
            queryable = queryable.Where(entity => entity.AssigneeUserId == assigneeUserId);
        }

        var now = DateTimeOffset.UtcNow;
        queryable = queue?.ToLowerInvariant() switch
        {
            "my-open" => queryable.Where(entity => entity.AssigneeUserId == userId && !entity.WorkflowStatus.IsTerminal),
            "unassigned" => queryable.Where(entity => entity.AssigneeUserId == null && !entity.WorkflowStatus.IsTerminal),
            "recently-updated" => queryable.Where(entity => entity.UpdatedAt >= now.AddDays(-7)),
            "todo" => queryable.Where(entity => entity.WorkflowStatus.Category == WorkflowStatusCategory.ToDo),
            "in-progress" => queryable.Where(entity => entity.WorkflowStatus.Category == WorkflowStatusCategory.InProgress),
            "overdue" => queryable.Where(entity => entity.DueAt != null && entity.DueAt < now && entity.ResolvedAt == null),
            "sla-risk" => queryable.Where(entity => entity.SlaState == SlaState.AtRisk || entity.SlaState == SlaState.Breached),
            "done-recently" => queryable.Where(entity => entity.ResolvedAt >= now.AddDays(-14)),
            _ => queryable,
        };

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            queryable = queryable.Where(entity =>
                EF.Functions.ILike(entity.Key, pattern) ||
                EF.Functions.ILike(entity.Title, pattern) ||
                EF.Functions.ILike(entity.DescriptionPlainText, pattern) ||
                entity.Labels.Any(label => EF.Functions.ILike(label, pattern)) ||
                entity.Comments.Any(comment => !comment.IsDeleted && !comment.IsHidden &&
                                               EF.Functions.ILike(comment.BodyPlainText, pattern)));
        }

        var total = await queryable.CountAsync(cancellationToken);
        var exactKey = normalizedSearch?.ToUpperInvariant();
        var items = await queryable
            .OrderByDescending(entity => exactKey != null && entity.Key == exactKey)
            .ThenByDescending(entity => entity.UpdatedAt)
            .Skip((requestedPage - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .Select(entity => new TicketListItemResponse(
                entity.Id,
                entity.Key,
                entity.ProjectId,
                entity.Project.NameEnglish,
                entity.Project.NameFrench,
                entity.Title,
                entity.Type,
                entity.Priority,
                entity.WorkflowStatusId,
                entity.WorkflowStatus.Key,
                entity.WorkflowStatus.LabelEnglish,
                entity.WorkflowStatus.LabelFrench,
                entity.AssigneeUserId,
                entity.AssigneeUser == null ? null : entity.AssigneeUser.DisplayName,
                entity.ReporterUser.DisplayName,
                entity.DueAt,
                entity.SlaState,
                entity.IsArchived,
                entity.UpdatedAt,
                entity.Version))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new PagedTicketResponse(items, requestedPage, requestedPageSize, total));
    }

    private static async Task<IResult> CreateAsync(
        CreateTicketRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer contentSanitizer,
        ITenantAuthorizationService authorizationService,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.CanMaintainTickets(request.ProjectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var creationKey = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        creationKey = string.IsNullOrWhiteSpace(creationKey) ? null : creationKey[..Math.Min(creationKey.Length, 120)];
        if (creationKey is not null)
        {
            var existing = await dbContext.Tickets.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.CreationKey == creationKey, cancellationToken);
            if (existing is not null)
            {
                return TypedResults.Ok(await LoadDetailAsync(dbContext, existing.Id, cancellationToken));
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 300)
        {
            return ApiProblems.Validation(httpContext, "A ticket title of up to 300 characters is required.");
        }

        var content = contentSanitizer.Sanitize(request.Description);
        if (string.IsNullOrWhiteSpace(content.PlainText))
        {
            return ApiProblems.Validation(httpContext, "A ticket description is required.");
        }

        if (request.AssigneeUserId is not null &&
            !await CanUseProjectAsync(authorizationService, request.AssigneeUserId.Value, authorization.TenantId, request.ProjectId, cancellationToken))
        {
            return ApiProblems.Validation(httpContext, "The selected assignee cannot access this project.", "invalid_assignee");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var project = await dbContext.Projects
            .FromSqlInterpolated($"SELECT * FROM projects WHERE id = {request.ProjectId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null || project.IsArchived)
        {
            return ApiProblems.NotFound(httpContext, "Active project");
        }

        var initialStatus = await dbContext.WorkflowStatuses
            .SingleOrDefaultAsync(entity => entity.WorkflowId == project.WorkflowId && entity.IsInitial, cancellationToken);
        if (initialStatus is null)
        {
            return ApiProblems.Conflict(httpContext, "The project workflow has no initial status.", "invalid_workflow");
        }

        var actorUserId = httpContext.User.UserId();
        var assigneeUserId = request.AssigneeUserId ?? project.DefaultAssigneeUserId;
        var now = DateTimeOffset.UtcNow;
        var ticket = new Ticket
        {
            TenantId = authorization.TenantId,
            ProjectId = project.Id,
            Number = project.NextTicketNumber,
            Key = $"{project.Key}-{project.NextTicketNumber}",
            CreationKey = creationKey,
            Title = request.Title.Trim(),
            Description = content.Html,
            DescriptionPlainText = content.PlainText,
            Type = request.Type,
            Priority = request.Priority,
            WorkflowStatusId = initialStatus.Id,
            ReporterUserId = actorUserId,
            AssigneeUserId = assigneeUserId,
            Labels = NormalizeLabels(request.Labels),
            DueAt = request.DueAt,
            FirstResponseTargetAt = project.FirstResponseTargetMinutes is null
                ? null
                : now.AddMinutes(project.FirstResponseTargetMinutes.Value),
            ResolutionTargetAt = project.ResolutionTargetMinutes is null
                ? null
                : now.AddMinutes(project.ResolutionTargetMinutes.Value),
            SlaState = project.ResolutionTargetMinutes is null ? SlaState.None : SlaState.OnTrack,
        };
        project.NextTicketNumber++;
        ticket.Watchers.Add(new TicketWatcher
        {
            TenantId = authorization.TenantId,
            TicketId = ticket.Id,
            Ticket = ticket,
            UserId = actorUserId,
        });
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            TenantId = authorization.TenantId,
            TicketId = ticket.Id,
            Ticket = ticket,
            ToStatusId = initialStatus.Id,
            ChangedByUserId = actorUserId,
        });
        dbContext.Tickets.Add(ticket);
        var audit = AddAudit(dbContext, httpContext, ticket, "ticket.created", "Ticket was created.");
        if (assigneeUserId is not null)
        {
            await notificationService.AddAsync(new NotificationMessage(
                authorization.TenantId,
                [assigneeUserId.Value],
                actorUserId,
                NotificationType.TicketAssigned,
                project.Id,
                ticket.Id,
                audit.Id.ToString(),
                $"{ticket.Key} was assigned to you.",
                $"{ticket.Key} vous a été attribué.",
                $"/app/tickets/{ticket.Id}",
                ticket.Id.ToString()), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Created($"/api/tickets/{ticket.Id}", await LoadDetailAsync(dbContext, ticket.Id, cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var projectId = await dbContext.Tickets.AsNoTracking()
            .Where(entity => entity.Id == ticketId)
            .Select(entity => (Guid?)entity.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
        if (projectId is null || !authorization.CanAccessProject(projectId.Value))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        return TypedResults.Ok(await LoadDetailAsync(dbContext, ticketId, cancellationToken));
    }

    private static async Task<IResult> UpdateAsync(
        Guid ticketId,
        UpdateTicketRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer contentSanitizer,
        ITenantAuthorizationService authorizationService,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(entity => entity.Id == ticketId, cancellationToken);
        if (ticket is null || !authorization.CanAccessProject(ticket.ProjectId))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        if (!authorization.CanMaintainTickets(ticket.ProjectId) || ticket.IsArchived)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 300)
        {
            return ApiProblems.Validation(httpContext, "A ticket title of up to 300 characters is required.");
        }

        var actorUserId = httpContext.User.UserId();
        var isManager = authorization.CanManageProject(ticket.ProjectId);
        if (request.AssigneeUserId != ticket.AssigneeUserId)
        {
            if (!isManager && request.AssigneeUserId != actorUserId)
            {
                return ApiProblems.Forbidden(httpContext);
            }

            if (request.AssigneeUserId is not null &&
                !await CanUseProjectAsync(
                    authorizationService,
                    request.AssigneeUserId.Value,
                    authorization.TenantId,
                    ticket.ProjectId,
                    cancellationToken))
            {
                return ApiProblems.Validation(httpContext, "The selected assignee cannot access this project.", "invalid_assignee");
            }
        }

        var content = contentSanitizer.Sanitize(request.Description);
        var assignmentChanged = ticket.AssigneeUserId != request.AssigneeUserId;
        var priorityChanged = ticket.Priority != request.Priority;
        var normalizedLabels = NormalizeLabels(request.Labels);
        var resolutionSummary = request.ResolutionSummary?.Trim();
        var recipients = priorityChanged
            ? await NotificationRecipientsAsync(dbContext, ticket, cancellationToken)
            : [];
        dbContext.Entry(ticket).State = EntityState.Detached;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var affected = await dbContext.Tickets
            .Where(entity => entity.Id == ticketId && entity.Version == request.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.Title, request.Title.Trim())
                .SetProperty(entity => entity.Description, content.Html)
                .SetProperty(entity => entity.DescriptionPlainText, content.PlainText)
                .SetProperty(entity => entity.Type, request.Type)
                .SetProperty(entity => entity.Priority, request.Priority)
                .SetProperty(entity => entity.AssigneeUserId, request.AssigneeUserId)
                .SetProperty(entity => entity.Labels, normalizedLabels)
                .SetProperty(entity => entity.DueAt, request.DueAt)
                .SetProperty(entity => entity.ResolutionSummary, resolutionSummary)
                .SetProperty(entity => entity.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        if (affected == 0)
        {
            return ApiProblems.Conflict(
                httpContext,
                "This ticket was changed by another user. Reload it before saving.",
                "ticket_concurrency_conflict");
        }

        ticket.Title = request.Title.Trim();
        ticket.Description = content.Html;
        ticket.DescriptionPlainText = content.PlainText;
        ticket.Type = request.Type;
        ticket.Priority = request.Priority;
        ticket.AssigneeUserId = request.AssigneeUserId;
        ticket.Labels = normalizedLabels;
        ticket.DueAt = request.DueAt;
        ticket.ResolutionSummary = resolutionSummary;
        var audit = AddAudit(dbContext, httpContext, ticket, "ticket.updated", "Ticket details were updated.");
        if (assignmentChanged && ticket.AssigneeUserId is not null)
        {
            await notificationService.AddAsync(new NotificationMessage(
                ticket.TenantId,
                [ticket.AssigneeUserId.Value],
                actorUserId,
                NotificationType.TicketAssigned,
                ticket.ProjectId,
                ticket.Id,
                audit.Id.ToString(),
                $"{ticket.Key} was assigned to you.",
                $"{ticket.Key} vous a été attribué.",
                $"/app/tickets/{ticket.Id}",
                ticket.Id.ToString()), cancellationToken);
        }

        if (priorityChanged)
        {
            await notificationService.AddAsync(new NotificationMessage(
                ticket.TenantId,
                recipients,
                actorUserId,
                NotificationType.TicketPriorityChanged,
                ticket.ProjectId,
                ticket.Id,
                $"{audit.Id}:priority",
                $"{ticket.Key} priority changed to {ticket.Priority}.",
                $"La priorité de {ticket.Key} est maintenant {ticket.Priority}.",
                $"/app/tickets/{ticket.Id}",
                ticket.Id.ToString()), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(await LoadDetailAsync(dbContext, ticket.Id, cancellationToken));
    }

    private static async Task<IResult> TransitionAsync(
        Guid ticketId,
        TransitionTicketRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var ticket = await dbContext.Tickets
            .Include(entity => entity.Project)
            .Include(entity => entity.WorkflowStatus)
            .SingleOrDefaultAsync(entity => entity.Id == ticketId, cancellationToken);
        if (ticket is null || !authorization.CanAccessProject(ticket.ProjectId))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        if (!authorization.CanMaintainTickets(ticket.ProjectId) || ticket.IsArchived || ticket.Project.IsArchived)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var transition = await dbContext.WorkflowTransitions
            .Include(entity => entity.ToStatus)
            .SingleOrDefaultAsync(entity =>
                entity.WorkflowId == ticket.Project.WorkflowId &&
                entity.FromStatusId == ticket.WorkflowStatusId &&
                entity.ToStatusId == request.ToStatusId,
                cancellationToken);
        if (transition is null)
        {
            return ApiProblems.Conflict(httpContext, "This workflow transition is not allowed.", "transition_not_allowed");
        }

        if (transition.CommentRequired && string.IsNullOrWhiteSpace(request.Comment))
        {
            return ApiProblems.Validation(httpContext, "A transition comment is required.", "transition_comment_required");
        }

        var actorUserId = httpContext.User.UserId();
        var previousStatusId = ticket.WorkflowStatusId;
        var wasTerminal = ticket.WorkflowStatus.IsTerminal;
        DateTimeOffset? resolvedAt = ticket.ResolvedAt;
        var slaState = ticket.SlaState;
        if (!wasTerminal && transition.ToStatus.IsTerminal)
        {
            resolvedAt = DateTimeOffset.UtcNow;
            slaState = ticket.ResolutionTargetAt is null
                ? SlaState.None
                : resolvedAt <= ticket.ResolutionTargetAt ? SlaState.Met : SlaState.Breached;
        }
        else if (wasTerminal && !transition.ToStatus.IsTerminal)
        {
            resolvedAt = null;
            slaState = ticket.ResolutionTargetAt is null ? SlaState.None : SlaState.OnTrack;
        }

        var recipients = await NotificationRecipientsAsync(dbContext, ticket, cancellationToken);
        dbContext.Entry(ticket).State = EntityState.Detached;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var affected = await dbContext.Tickets
            .Where(entity => entity.Id == ticketId && entity.Version == request.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.WorkflowStatusId, transition.ToStatusId)
                .SetProperty(entity => entity.ResolvedAt, resolvedAt)
                .SetProperty(entity => entity.SlaState, slaState)
                .SetProperty(entity => entity.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        if (affected == 0)
        {
            return ApiProblems.Conflict(
                httpContext,
                "This ticket was changed by another user. Reload it before changing status.",
                "ticket_concurrency_conflict");
        }

        ticket.WorkflowStatusId = transition.ToStatusId;
        ticket.ResolvedAt = resolvedAt;
        ticket.SlaState = slaState;
        dbContext.TicketStatusHistory.Add(new TicketStatusHistory
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            FromStatusId = previousStatusId,
            ToStatusId = transition.ToStatusId,
            ChangedByUserId = actorUserId,
            Comment = request.Comment?.Trim(),
        });
        var audit = AddAudit(dbContext, httpContext, ticket, "ticket.transitioned", "Ticket status was changed.");
        var notificationType = transition.ToStatus.IsTerminal
            ? NotificationType.TicketResolved
            : wasTerminal ? NotificationType.TicketReopened : NotificationType.TicketStatusChanged;
        await notificationService.AddAsync(new NotificationMessage(
            ticket.TenantId,
            recipients,
            actorUserId,
            notificationType,
            ticket.ProjectId,
            ticket.Id,
            audit.Id.ToString(),
            $"{ticket.Key} moved to {transition.ToStatus.LabelEnglish}.",
            $"{ticket.Key} est passé à {transition.ToStatus.LabelFrench}.",
            $"/app/tickets/{ticket.Id}",
            ticket.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(await LoadDetailAsync(dbContext, ticket.Id, cancellationToken));
    }

    private static Task<IResult> ArchiveAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetArchivedAsync(ticketId, true, httpContext, dbContext, cancellationToken);

    private static Task<IResult> RestoreAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetArchivedAsync(ticketId, false, httpContext, dbContext, cancellationToken);

    private static async Task<IResult> SetArchivedAsync(
        Guid ticketId,
        bool archived,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(entity => entity.Id == ticketId, cancellationToken);
        if (ticket is null || !authorization.CanAccessProject(ticket.ProjectId))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        if (!authorization.CanManageProject(ticket.ProjectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        ticket.IsArchived = archived;
        AddAudit(
            dbContext,
            httpContext,
            ticket,
            archived ? "ticket.archived" : "ticket.restored",
            archived ? "Ticket was archived." : "Ticket was restored.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadDetailAsync(dbContext, ticket.Id, cancellationToken));
    }

    private static async Task<IResult> BulkUpdateAsync(
        BulkTicketUpdateRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (request.TicketIds.Count is 0 or > 100)
        {
            return ApiProblems.Validation(httpContext, "Select between 1 and 100 tickets.");
        }

        var tickets = await dbContext.Tickets.Where(entity => request.TicketIds.Contains(entity.Id)).ToArrayAsync(cancellationToken);
        var denied = request.TicketIds.Except(tickets.Select(entity => entity.Id))
            .Concat(tickets.Where(entity => !authorization.CanManageProject(entity.ProjectId)).Select(entity => entity.Id))
            .Distinct()
            .ToArray();
        if (denied.Length > 0)
        {
            return TypedResults.Ok(new BulkTicketUpdateResponse(0, denied.Select(id => new BulkTicketFailure(id, "not_found_or_forbidden")).ToArray()));
        }

        foreach (var ticket in tickets)
        {
            if (request.Priority is not null)
            {
                ticket.Priority = request.Priority.Value;
            }

            if (request.AssigneeSpecified)
            {
                ticket.AssigneeUserId = request.AssigneeUserId;
            }

            if (request.Labels is not null)
            {
                ticket.Labels = NormalizeLabels(request.Labels);
            }

            if (request.Archived is not null)
            {
                ticket.IsArchived = request.Archived.Value;
            }

            AddAudit(dbContext, httpContext, ticket, "ticket.bulk_updated", "Ticket was updated by a bulk action.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new BulkTicketUpdateResponse(tickets.Length, []));
    }

    private static async Task<TicketDetailResponse> LoadDetailAsync(
        ReqNestDbContext dbContext,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        await dbContext.Tickets.AsNoTracking()
            .Where(entity => entity.Id == ticketId)
            .Select(entity => new TicketDetailResponse(
                entity.Id,
                entity.Key,
                entity.ProjectId,
                entity.Project.Key,
                entity.Project.NameEnglish,
                entity.Project.NameFrench,
                entity.Title,
                entity.Description,
                entity.Type,
                entity.Priority,
                entity.WorkflowStatusId,
                entity.WorkflowStatus.Key,
                entity.WorkflowStatus.LabelEnglish,
                entity.WorkflowStatus.LabelFrench,
                entity.WorkflowStatus.Category,
                entity.ReporterUserId,
                entity.ReporterUser.DisplayName,
                entity.AssigneeUserId,
                entity.AssigneeUser == null ? null : entity.AssigneeUser.DisplayName,
                entity.Labels,
                entity.DueAt,
                entity.FirstRespondedAt,
                entity.ResolvedAt,
                entity.FirstResponseTargetAt,
                entity.ResolutionTargetAt,
                entity.SlaState,
                entity.ResolutionSummary,
                entity.IsArchived,
                entity.Watchers.Select(watcher => new TicketWatcherResponse(
                    watcher.UserId,
                    watcher.User.DisplayName,
                    watcher.IsMuted)).ToArray(),
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.Version))
            .SingleAsync(cancellationToken);

    internal static async Task<bool> CanUseProjectAsync(
        ITenantAuthorizationService authorizationService,
        Guid userId,
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.GetAuthorizationAsync(userId, tenantId, cancellationToken);
        return authorization?.CanAccessProject(projectId) == true;
    }

    internal static async Task<Guid[]> NotificationRecipientsAsync(
        ReqNestDbContext dbContext,
        Ticket ticket,
        CancellationToken cancellationToken) =>
        await dbContext.TicketWatchers
            .Where(entity => entity.TicketId == ticket.Id && !entity.IsMuted)
            .Select(entity => entity.UserId)
            .Concat(dbContext.Tickets.Where(entity => entity.Id == ticket.Id).Select(entity => entity.ReporterUserId))
            .Concat(dbContext.Tickets.Where(entity => entity.Id == ticket.Id && entity.AssigneeUserId != null)
                .Select(entity => entity.AssigneeUserId!.Value))
            .Distinct()
            .ToArrayAsync(cancellationToken);

    internal static AuditEvent AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Ticket ticket,
        string action,
        string summary)
    {
        var audit = new AuditEvent
        {
            TenantId = ticket.TenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = nameof(Ticket),
            TargetId = ticket.Id.ToString(),
            Summary = summary,
            CorrelationId = context.TraceIdentifier,
        };
        dbContext.AuditEvents.Add(audit);
        return audit;
    }

    private static string[] NormalizeLabels(IReadOnlyCollection<string> labels) =>
        labels.Select(entity => entity.Trim().ToLowerInvariant())
            .Where(entity => entity.Length is > 0 and <= 40)
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToArray();
}

public sealed record CreateTicketRequest(
    Guid ProjectId,
    string Title,
    string Description,
    TicketType Type,
    TicketPriority Priority,
    Guid? AssigneeUserId,
    IReadOnlyCollection<string> Labels,
    DateTimeOffset? DueAt);

public sealed record UpdateTicketRequest(
    string Title,
    string Description,
    TicketType Type,
    TicketPriority Priority,
    Guid? AssigneeUserId,
    IReadOnlyCollection<string> Labels,
    DateTimeOffset? DueAt,
    string? ResolutionSummary,
    uint Version);

public sealed record TransitionTicketRequest(Guid ToStatusId, string? Comment, uint Version);

public sealed record BulkTicketUpdateRequest(
    IReadOnlyCollection<Guid> TicketIds,
    TicketPriority? Priority,
    bool AssigneeSpecified,
    Guid? AssigneeUserId,
    IReadOnlyCollection<string>? Labels,
    bool? Archived);

public sealed record BulkTicketFailure(Guid TicketId, string Code);

public sealed record BulkTicketUpdateResponse(int Updated, IReadOnlyCollection<BulkTicketFailure> Failures);

public sealed record TicketWatcherResponse(Guid UserId, string DisplayName, bool IsMuted);

public sealed record TicketListItemResponse(
    Guid Id,
    string Key,
    Guid ProjectId,
    string ProjectNameEnglish,
    string ProjectNameFrench,
    string Title,
    TicketType Type,
    TicketPriority Priority,
    Guid StatusId,
    string StatusKey,
    string StatusLabelEnglish,
    string StatusLabelFrench,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    string ReporterDisplayName,
    DateTimeOffset? DueAt,
    SlaState SlaState,
    bool IsArchived,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record PagedTicketResponse(
    IReadOnlyCollection<TicketListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record TicketDetailResponse(
    Guid Id,
    string Key,
    Guid ProjectId,
    string ProjectKey,
    string ProjectNameEnglish,
    string ProjectNameFrench,
    string Title,
    string Description,
    TicketType Type,
    TicketPriority Priority,
    Guid StatusId,
    string StatusKey,
    string StatusLabelEnglish,
    string StatusLabelFrench,
    WorkflowStatusCategory StatusCategory,
    Guid ReporterUserId,
    string ReporterDisplayName,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    IReadOnlyCollection<string> Labels,
    DateTimeOffset? DueAt,
    DateTimeOffset? FirstRespondedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? FirstResponseTargetAt,
    DateTimeOffset? ResolutionTargetAt,
    SlaState SlaState,
    string? ResolutionSummary,
    bool IsArchived,
    IReadOnlyCollection<TicketWatcherResponse> Watchers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
