using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Notifications;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .RequireAuthorization()
            .WithTags("Notifications");
        group.MapGet("/", ListAsync);
        group.MapPatch("/{notificationId:guid}", SetReadStateAsync);
        group.MapPost("/mark-all-read", MarkAllReadAsync);
        group.MapGet("/preferences", GetPreferencesAsync);
        group.MapPut("/preferences", UpdatePreferencesAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid? projectId,
        NotificationType? type,
        bool? unread,
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

        var userId = httpContext.User.UserId();
        var accessibleProjectIds = authorization.ProjectRoles.Keys.ToArray();
        var allProjects = authorization.AllProjectRoles.Count > 0;
        var query = dbContext.Notifications.AsNoTracking().Where(entity =>
            entity.RecipientUserId == userId &&
            (entity.ProjectId == null || allProjects || accessibleProjectIds.Contains(entity.ProjectId.Value)));
        if (projectId is not null)
        {
            if (!authorization.CanAccessProject(projectId.Value))
            {
                return ApiProblems.NotFound(httpContext, "Project");
            }

            query = query.Where(entity => entity.ProjectId == projectId);
        }

        if (type is not null)
        {
            query = query.Where(entity => entity.Type == type);
        }

        if (unread is not null)
        {
            query = unread.Value ? query.Where(entity => entity.ReadAt == null) : query.Where(entity => entity.ReadAt != null);
        }

        var requestedPage = Math.Max(1, page ?? 1);
        var requestedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var unreadCount = await dbContext.Notifications.CountAsync(
            entity => entity.RecipientUserId == userId && entity.ReadAt == null &&
                      (entity.ProjectId == null || allProjects || accessibleProjectIds.Contains(entity.ProjectId.Value)),
            cancellationToken);
        var items = await query.OrderByDescending(entity => entity.CreatedAt)
            .Skip((requestedPage - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .Select(entity => new NotificationResponse(
                entity.Id,
                entity.Type,
                entity.ActorUserId,
                entity.ProjectId,
                entity.TicketId,
                entity.SummaryEnglish,
                entity.SummaryFrench,
                entity.DeepLink,
                entity.ReadAt,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new PagedNotificationResponse(items, requestedPage, requestedPageSize, total, unreadCount));
    }

    private static async Task<IResult> SetReadStateAsync(
        Guid notificationId,
        SetNotificationReadRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            entity => entity.Id == notificationId && entity.RecipientUserId == httpContext.User.UserId(),
            cancellationToken);
        if (notification is null)
        {
            return ApiProblems.NotFound(httpContext, "Notification");
        }

        notification.ReadAt = request.Read ? DateTimeOffset.UtcNow : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> MarkAllReadAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        await dbContext.Notifications
            .Where(entity => entity.RecipientUserId == httpContext.User.UserId() && entity.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.ReadAt, DateTimeOffset.UtcNow), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetPreferencesAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var preferences = await dbContext.NotificationPreferences.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);
        return TypedResults.Ok(preferences is null
            ? new NotificationPreferencesResponse(true, true, true, false)
            : ToResponse(preferences));
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        NotificationPreferencesResponse request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var preferences = await dbContext.NotificationPreferences.SingleOrDefaultAsync(
            entity => entity.UserId == userId,
            cancellationToken);
        if (preferences is null)
        {
            preferences = new NotificationPreference
            {
                TenantId = authorization.TenantId,
                UserId = userId,
            };
            dbContext.NotificationPreferences.Add(preferences);
        }

        preferences.CommentsEnabled = request.CommentsEnabled;
        preferences.WatcherUpdatesEnabled = request.WatcherUpdatesEnabled;
        preferences.DueDateUpdatesEnabled = request.DueDateUpdatesEnabled;
        preferences.DigestEnabled = request.DigestEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(preferences));
    }

    private static NotificationPreferencesResponse ToResponse(NotificationPreference entity) => new(
        entity.CommentsEnabled,
        entity.WatcherUpdatesEnabled,
        entity.DueDateUpdatesEnabled,
        entity.DigestEnabled);
}

public sealed record SetNotificationReadRequest(bool Read);

public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    Guid? ActorUserId,
    Guid? ProjectId,
    Guid? TicketId,
    string SummaryEnglish,
    string SummaryFrench,
    string DeepLink,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record PagedNotificationResponse(
    IReadOnlyCollection<NotificationResponse> Items,
    int Page,
    int PageSize,
    int Total,
    int UnreadCount);

public sealed record NotificationPreferencesResponse(
    bool CommentsEnabled,
    bool WatcherUpdatesEnabled,
    bool DueDateUpdatesEnabled,
    bool DigestEnabled);
