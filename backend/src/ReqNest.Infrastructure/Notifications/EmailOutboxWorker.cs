using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReqNest.Core.Identity;
using ReqNest.Core.Notifications;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Notifications;

public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailOutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await ProcessDigestsAsync(stoppingToken);
                await DeliverPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Email outbox processing failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessDigestsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var now = DateTimeOffset.UtcNow;
        var duePreferences = await dbContext.NotificationPreferences.IgnoreQueryFilters()
            .Where(entity => entity.EmailEnabled && entity.DigestEnabled &&
                             (entity.LastDigestAt == null || entity.LastDigestAt < now.AddHours(-1)))
            .OrderBy(entity => entity.LastDigestAt)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        var tenantIds = duePreferences.Select(entity => entity.TenantId).Distinct().ToArray();
        var tenantTimeZones = await dbContext.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(entity => tenantIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.TimeZone, cancellationToken);
        foreach (var preference in duePreferences)
        {
            var timeZone = ResolveTimeZone(tenantTimeZones.GetValueOrDefault(preference.TenantId));
            var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
            var lastLocalDate = preference.LastDigestAt is null
                ? (DateOnly?)null
                : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(preference.LastDigestAt.Value, timeZone).DateTime);
            if (localNow.Hour != preference.DigestHourLocal ||
                lastLocalDate == DateOnly.FromDateTime(localNow.DateTime))
            {
                continue;
            }

            var user = await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(entity => entity.Id == preference.UserId, cancellationToken);
            var unread = await dbContext.Notifications.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(entity =>
                    entity.TenantId == preference.TenantId &&
                    entity.RecipientUserId == preference.UserId &&
                    entity.ReadAt == null,
                    cancellationToken);
            if (unread > 0)
            {
                var french = user.PreferredLanguage == AppLanguage.French;
                var dateKey = localNow.ToString("yyyy-MM-dd");
                dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage
                {
                    TenantId = preference.TenantId,
                    RecipientUserId = user.Id,
                    RecipientEmail = user.Email,
                    Subject = french ? "Votre résumé ReqNest" : "Your ReqNest digest",
                    BodyText = french
                        ? $"Vous avez {unread} notification(s) non lue(s). Ouvrez ReqNest pour les consulter."
                        : $"You have {unread} unread notification(s). Open ReqNest to review them.",
                    BodyHtml = french
                        ? $"<p>Vous avez <strong>{unread}</strong> notification(s) non lue(s).</p>"
                        : $"<p>You have <strong>{unread}</strong> unread notification(s).</p>",
                    TemplateKey = "notification.digest",
                    DeduplicationKey = $"digest:{preference.UserId}:{dateKey}",
                    Status = EmailOutboxStatus.Pending,
                });
            }

            preference.LastDigestAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private async Task DeliverPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IEmailDeliveryProvider>();
        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.EmailOutboxMessages.IgnoreQueryFilters()
            .Where(entity => entity.Status == EmailOutboxStatus.Pending && entity.NextAttemptAt <= now)
            .OrderBy(entity => entity.NextAttemptAt)
            .Take(50)
            .ToArrayAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                await provider.DeliverAsync(
                    new OutboundEmail(message.RecipientEmail, message.Subject, message.BodyText, message.BodyHtml),
                    cancellationToken);
                message.Status = EmailOutboxStatus.Sent;
                message.SentAt = now;
                message.LastError = null;
            }
            catch (Exception exception)
            {
                message.Attempts++;
                message.LastError = exception.GetType().Name;
                if (message.Attempts >= 5)
                {
                    message.Status = EmailOutboxStatus.Failed;
                }
                else
                {
                    message.NextAttemptAt = now.AddMinutes(Math.Pow(2, message.Attempts));
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
