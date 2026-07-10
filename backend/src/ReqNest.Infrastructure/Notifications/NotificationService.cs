using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Notifications;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Notifications;

public sealed class NotificationService(ReqNestDbContext dbContext) : INotificationService
{
    public async Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var recipientIds = message.RecipientUserIds
            .Where(userId => message.NotifyActor || userId != message.ActorUserId)
            .Distinct()
            .ToArray();
        if (recipientIds.Length == 0)
        {
            return;
        }

        var preferences = await dbContext.NotificationPreferences.IgnoreQueryFilters()
            .Where(entity => entity.TenantId == message.TenantId && recipientIds.Contains(entity.UserId))
            .ToDictionaryAsync(entity => entity.UserId, cancellationToken);
        recipientIds = recipientIds.Where(userId => preferences.TryGetValue(userId, out var preference)
            ? IsEnabled(preference, message.Type)
            : true).ToArray();
        if (recipientIds.Length == 0)
        {
            return;
        }

        var existingRecipients = await dbContext.Notifications
            .Where(entity => recipientIds.Contains(entity.RecipientUserId) && entity.EventKey == message.EventKey)
            .Select(entity => entity.RecipientUserId)
            .ToArrayAsync(cancellationToken);
        foreach (var recipientId in recipientIds.Except(existingRecipients))
        {
            dbContext.Notifications.Add(new Notification
            {
                TenantId = message.TenantId,
                RecipientUserId = recipientId,
                ActorUserId = message.ActorUserId,
                Type = message.Type,
                ProjectId = message.ProjectId,
                TicketId = message.TicketId,
                EventKey = message.EventKey,
                SummaryEnglish = message.SummaryEnglish,
                SummaryFrench = message.SummaryFrench,
                DeepLink = message.DeepLink,
                GroupKey = message.GroupKey,
            });
        }
    }

    private static bool IsEnabled(NotificationPreference preference, NotificationType type) => type switch
    {
        NotificationType.TicketCommented => preference.CommentsEnabled,
        NotificationType.TicketStatusChanged or NotificationType.TicketPriorityChanged => preference.WatcherUpdatesEnabled,
        NotificationType.DueDateApproaching or NotificationType.DueDatePassed => preference.DueDateUpdatesEnabled,
        _ => true,
    };
}
