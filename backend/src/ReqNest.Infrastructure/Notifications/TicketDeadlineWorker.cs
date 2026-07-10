using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReqNest.Core.Auditing;
using ReqNest.Core.Notifications;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Notifications;

public sealed class TicketDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TicketDeadlineWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ticket deadline notification processing failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidates = await dbContext.Tickets.IgnoreQueryFilters()
            .Where(entity => !entity.IsArchived && entity.ResolvedAt == null && entity.SlaPausedAt == null &&
                             (entity.DueAt != null && entity.DueAt <= now.AddHours(24) ||
                              entity.SlaWarningAt != null && entity.SlaWarningAt <= now ||
                              entity.ResolutionTargetAt != null && entity.ResolutionTargetAt <= now))
            .OrderBy(entity => entity.DueAt ?? entity.ResolutionTargetAt)
            .Take(1_000)
            .ToArrayAsync(cancellationToken);
        foreach (var ticket in candidates)
        {
            var recipients = await dbContext.TicketWatchers.IgnoreQueryFilters()
                .Where(entity => entity.TicketId == ticket.Id && !entity.IsMuted)
                .Select(entity => entity.UserId)
                .Concat(dbContext.Tickets.IgnoreQueryFilters().Where(entity => entity.Id == ticket.Id)
                    .Select(entity => entity.ReporterUserId))
                .Concat(dbContext.Tickets.IgnoreQueryFilters().Where(entity => entity.Id == ticket.Id && entity.AssigneeUserId != null)
                    .Select(entity => entity.AssigneeUserId!.Value))
                .Distinct()
                .ToArrayAsync(cancellationToken);

            if (ticket.DueAt is not null && ticket.DueAt <= now)
            {
                AddNotifications(dbContext, ticket, recipients, NotificationType.DueDatePassed,
                    $"due-passed:{ticket.Id}:{ticket.DueAt:O}", $"{ticket.Key} is overdue.", $"{ticket.Key} est en retard.");
            }
            else if (ticket.DueAt is not null)
            {
                AddNotifications(dbContext, ticket, recipients, NotificationType.DueDateApproaching,
                    $"due-warning:{ticket.Id}:{ticket.DueAt:O}", $"{ticket.Key} is due soon.", $"{ticket.Key} arrive bientôt à échéance.");
            }

            if (ticket.ResolutionTargetAt is not null && ticket.ResolutionTargetAt <= now)
            {
                var changed = ticket.SlaState != SlaState.Breached;
                ticket.SlaState = SlaState.Breached;
                AddNotifications(dbContext, ticket, recipients, NotificationType.SlaBreached,
                    $"sla-breached:{ticket.Id}:{ticket.ResolutionTargetAt:O}", $"{ticket.Key} breached its SLA.", $"{ticket.Key} a dépassé son SLA.");
                if (changed)
                {
                    AddSlaAudit(dbContext, ticket, "ticket.sla.breached", "The ticket breached its SLA target.");
                }
            }
            else if (ticket.SlaWarningAt is not null && ticket.SlaWarningAt <= now)
            {
                var changed = ticket.SlaState != SlaState.AtRisk;
                ticket.SlaState = SlaState.AtRisk;
                AddNotifications(dbContext, ticket, recipients, NotificationType.SlaAtRisk,
                    $"sla-risk:{ticket.Id}:{ticket.ResolutionTargetAt:O}", $"{ticket.Key} is at risk of breaching its SLA.", $"{ticket.Key} risque de dépasser son SLA.");
                if (changed)
                {
                    AddSlaAudit(dbContext, ticket, "ticket.sla.at-risk", "The ticket entered its SLA warning window.");
                }
            }
        }

        var pending = dbContext.ChangeTracker.Entries<Notification>()
            .Where(entry => entry.State == EntityState.Added)
            .ToArray();
        if (pending.Length == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var eventKeys = pending.Select(entry => entry.Entity.EventKey).Distinct().ToArray();
        var existing = (await dbContext.Notifications.IgnoreQueryFilters()
                .Where(entity => eventKeys.Contains(entity.EventKey))
                .Select(entity => new { entity.RecipientUserId, entity.EventKey })
                .ToArrayAsync(cancellationToken))
            .Select(item => $"{item.RecipientUserId:N}:{item.EventKey}")
            .ToHashSet(StringComparer.Ordinal);
        var recipientIds = pending.Select(entry => entry.Entity.RecipientUserId).Distinct().ToArray();
        var preferences = await dbContext.NotificationPreferences.IgnoreQueryFilters()
            .Where(entity => recipientIds.Contains(entity.UserId))
            .ToDictionaryAsync(entity => (entity.TenantId, entity.UserId), cancellationToken);
        foreach (var entry in pending)
        {
            var notification = entry.Entity;
            var duplicate = existing.Contains($"{notification.RecipientUserId:N}:{notification.EventKey}");
            var disabled = notification.Type is NotificationType.DueDateApproaching or NotificationType.DueDatePassed &&
                           preferences.TryGetValue((notification.TenantId, notification.RecipientUserId), out var preference) &&
                           !preference.DueDateUpdatesEnabled;
            if (duplicate || disabled)
            {
                entry.State = EntityState.Detached;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AddNotifications(
        ReqNestDbContext dbContext,
        Ticket ticket,
        IEnumerable<Guid> recipients,
        NotificationType type,
        string eventKey,
        string english,
        string french)
    {
        foreach (var recipientId in recipients)
        {
            dbContext.Notifications.Add(new Notification
            {
                TenantId = ticket.TenantId,
                RecipientUserId = recipientId,
                Type = type,
                ProjectId = ticket.ProjectId,
                TicketId = ticket.Id,
                EventKey = eventKey,
                SummaryEnglish = english,
                SummaryFrench = french,
                DeepLink = $"/app/tickets/{ticket.Id}",
                GroupKey = ticket.Id.ToString(),
            });
        }
    }

    private static void AddSlaAudit(
        ReqNestDbContext dbContext,
        Ticket ticket,
        string action,
        string summary)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = ticket.TenantId,
            Action = action,
            TargetType = nameof(Ticket),
            TargetId = ticket.Id.ToString(),
            Summary = summary,
            CorrelationId = "sla-worker",
        });
    }
}
