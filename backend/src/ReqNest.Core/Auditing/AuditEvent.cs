using ReqNest.Core.Common;

namespace ReqNest.Core.Auditing;

public sealed class AuditEvent : Entity
{
    public Guid TenantId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }
}
