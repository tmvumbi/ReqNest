namespace ReqNest.Core.Notifications;

public sealed record NotificationMessage(
    Guid TenantId,
    IReadOnlyCollection<Guid> RecipientUserIds,
    Guid? ActorUserId,
    NotificationType Type,
    Guid? ProjectId,
    Guid? TicketId,
    string EventKey,
    string SummaryEnglish,
    string SummaryFrench,
    string DeepLink,
    string? GroupKey = null,
    bool NotifyActor = false);

public interface INotificationService
{
    Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
