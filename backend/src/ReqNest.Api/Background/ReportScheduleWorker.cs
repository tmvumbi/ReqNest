using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReqNest.Api.Endpoints;
using ReqNest.Core.Identity;
using ReqNest.Core.Notifications;
using ReqNest.Core.Reports;
using ReqNest.Core.Storage;
using ReqNest.Core.Tenancy;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;

namespace ReqNest.Api.Background;

public sealed class ReportScheduleWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReportScheduleWorker> logger)
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
                logger.LogError(exception, "Scheduled report processing failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        Guid[] dueIds;
        await using (var discoveryScope = scopeFactory.CreateAsyncScope())
        {
            var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
            dueIds = await discoveryDb.ReportSchedules.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(entity => entity.IsActive && entity.NextRunAt <= DateTimeOffset.UtcNow)
                .OrderBy(entity => entity.NextRunAt)
                .Take(100)
                .Select(entity => entity.Id)
                .ToArrayAsync(cancellationToken);
        }

        foreach (var scheduleId in dueIds)
        {
            await ProcessOneAsync(scheduleId, cancellationToken);
        }
    }

    private async Task ProcessOneAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ReqNestDbContext>();
        var scheduleIdentity = await dbContext.ReportSchedules.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.Id == scheduleId)
            .Select(entity => new { entity.TenantId, entity.OwnerUserId, entity.Name, entity.ProjectId })
            .SingleOrDefaultAsync(cancellationToken);
        if (scheduleIdentity is null)
        {
            return;
        }

        var tenantContext = services.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = scheduleIdentity.TenantId;
        tenantContext.UserId = scheduleIdentity.OwnerUserId;
        var authorization = await services.GetRequiredService<ITenantAuthorizationService>()
            .GetAuthorizationAsync(scheduleIdentity.OwnerUserId, scheduleIdentity.TenantId, cancellationToken);
        if (authorization is null)
        {
            await dbContext.ReportSchedules.IgnoreQueryFilters()
                .Where(entity => entity.Id == scheduleId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsActive, false), cancellationToken);
            return;
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, scheduleIdentity.OwnerUserId.ToString())],
            "scheduled-report"));
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = principal,
            TraceIdentifier = $"report-schedule:{scheduleId:N}",
        };
        context.Items[nameof(TenantAuthorization)] = authorization;

        try
        {
            var result = await ReportEndpoints.RunScheduleAsync(
                scheduleId,
                context,
                dbContext,
                services.GetRequiredService<IReportPdfGenerator>(),
                services.GetRequiredService<IBlobStorageService>(),
                services.GetRequiredService<IOptions<BlobStorageOptions>>(),
                services.GetRequiredService<INotificationService>(),
                cancellationToken);
            if (result is IStatusCodeHttpResult { StatusCode: >= 400 })
            {
                await NotifyFailureAsync(
                    dbContext,
                    services.GetRequiredService<INotificationService>(),
                    scheduleIdentity.TenantId,
                    scheduleIdentity.OwnerUserId,
                    scheduleIdentity.ProjectId,
                    scheduleId,
                    scheduleIdentity.Name,
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled report {ScheduleId} failed.", scheduleId);
            dbContext.ChangeTracker.Clear();
            await NotifyFailureAsync(
                dbContext,
                services.GetRequiredService<INotificationService>(),
                scheduleIdentity.TenantId,
                scheduleIdentity.OwnerUserId,
                scheduleIdentity.ProjectId,
                scheduleId,
                scheduleIdentity.Name,
                cancellationToken);
        }
    }

    private static async Task NotifyFailureAsync(
        ReqNestDbContext dbContext,
        INotificationService notificationService,
        Guid tenantId,
        Guid ownerUserId,
        Guid? projectId,
        Guid scheduleId,
        string scheduleName,
        CancellationToken cancellationToken)
    {
        var failedAt = DateTimeOffset.UtcNow;
        await notificationService.AddAsync(new NotificationMessage(
            tenantId,
            [ownerUserId],
            ownerUserId,
            NotificationType.ReportFailed,
            projectId,
            null,
            $"report-schedule-failed:{scheduleId}:{failedAt:yyyyMMddHHmm}",
            $"Scheduled report '{scheduleName}' failed.",
            $"Le rapport planifié « {scheduleName} » a échoué.",
            "/app/reports",
            scheduleId.ToString(),
            NotifyActor: true), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
