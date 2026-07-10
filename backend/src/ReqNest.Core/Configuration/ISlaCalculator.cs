using ReqNest.Core.Tickets;

namespace ReqNest.Core.Configuration;

public sealed record SlaSnapshot(
    Guid PolicyId,
    string PolicyName,
    DateTimeOffset FirstResponseTargetAt,
    DateTimeOffset ResolutionTargetAt,
    DateTimeOffset WarningAt);

public interface ISlaCalculator
{
    Task<SlaSnapshot?> CalculateAsync(
        Guid projectId,
        string priorityKey,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken = default);

    Task ApplyPauseStateAsync(
        Ticket ticket,
        string destinationStatusKey,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default);
}
