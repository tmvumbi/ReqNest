using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Configuration;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Configuration;

public sealed class SlaCalculator(ReqNestDbContext dbContext) : ISlaCalculator
{
    public async Task<SlaSnapshot?> CalculateAsync(
        Guid projectId,
        string priorityKey,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.AsNoTracking()
            .Where(entity => entity.Id == projectId)
            .Select(entity => new { entity.SlaPolicyId })
            .SingleAsync(cancellationToken);
        var policies = dbContext.SlaPolicies.AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.Targets)
            .Include(entity => entity.Holidays)
            .Where(entity => entity.IsActive);
        var policy = project.SlaPolicyId is not null
            ? await policies.SingleOrDefaultAsync(entity => entity.Id == project.SlaPolicyId, cancellationToken)
            : await policies
                .OrderByDescending(entity => entity.ProjectId == projectId)
                .ThenByDescending(entity => entity.IsDefault)
                .FirstOrDefaultAsync(entity => entity.ProjectId == projectId || entity.ProjectId == null, cancellationToken);
        var target = policy?.Targets.SingleOrDefault(entity => entity.PriorityKey == priorityKey);
        if (policy is null || target is null)
        {
            return null;
        }

        var firstResponse = AddBusinessMinutes(policy, startsAt, target.FirstResponseMinutes);
        var resolution = AddBusinessMinutes(policy, startsAt, target.ResolutionMinutes);
        var warning = resolution.AddMinutes(-Math.Min(policy.WarningMinutesBefore, target.ResolutionMinutes));
        return new SlaSnapshot(policy.Id, policy.Name, firstResponse, resolution, warning);
    }

    public async Task ApplyPauseStateAsync(
        Ticket ticket,
        string destinationStatusKey,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default)
    {
        if (ticket.SlaPolicyId is null)
        {
            return;
        }

        var pauseStatusKeys = await dbContext.SlaPolicies.AsNoTracking()
            .Where(entity => entity.Id == ticket.SlaPolicyId)
            .Select(entity => entity.PauseStatusKeys)
            .SingleOrDefaultAsync(cancellationToken) ?? [];
        var shouldPause = pauseStatusKeys.Contains(destinationStatusKey, StringComparer.Ordinal);
        if (shouldPause && ticket.SlaPausedAt is null)
        {
            ticket.SlaPausedAt = changedAt;
            return;
        }

        if (!shouldPause && ticket.SlaPausedAt is not null)
        {
            var pausedMinutes = Math.Max(0, (int)Math.Ceiling((changedAt - ticket.SlaPausedAt.Value).TotalMinutes));
            ticket.SlaPausedMinutes += pausedMinutes;
            ticket.FirstResponseTargetAt = ticket.FirstResponseTargetAt?.AddMinutes(pausedMinutes);
            ticket.ResolutionTargetAt = ticket.ResolutionTargetAt?.AddMinutes(pausedMinutes);
            ticket.SlaWarningAt = ticket.SlaWarningAt?.AddMinutes(pausedMinutes);
            ticket.SlaPausedAt = null;
        }
    }

    private static DateTimeOffset AddBusinessMinutes(SlaPolicy policy, DateTimeOffset startsAt, int minutes)
    {
        var timeZone = ResolveTimeZone(policy.TimeZone);
        var local = TimeZoneInfo.ConvertTime(startsAt, timeZone).DateTime;
        var remaining = Math.Max(0, minutes);
        var holidays = policy.Holidays.Select(entity => entity.Date).ToHashSet();
        for (var dayGuard = 0; dayGuard < 3660; dayGuard++)
        {
            var day = DateOnly.FromDateTime(local);
            var isWorkingDay = (policy.WorkingDaysMask & (1 << (int)local.DayOfWeek)) != 0;
            if (isWorkingDay && !holidays.Contains(day))
            {
                var windowStart = local.Date.AddMinutes(policy.BusinessDayStartMinutes);
                var windowEnd = local.Date.AddMinutes(policy.BusinessDayEndMinutes);
                if (local < windowStart)
                {
                    local = windowStart;
                }

                if (local < windowEnd)
                {
                    var available = (int)Math.Floor((windowEnd - local).TotalMinutes);
                    if (remaining <= available)
                    {
                        return ToOffset(local.AddMinutes(remaining), timeZone);
                    }

                    remaining -= available;
                }
            }

            local = local.Date.AddDays(1).AddMinutes(policy.BusinessDayStartMinutes);
        }

        throw new InvalidOperationException("The SLA calendar could not produce a target within ten years.");
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
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

    private static DateTimeOffset ToOffset(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }
}
