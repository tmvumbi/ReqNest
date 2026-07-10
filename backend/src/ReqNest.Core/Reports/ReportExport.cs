using ReqNest.Core.Common;
using ReqNest.Core.Identity;

namespace ReqNest.Core.Reports;

public enum ReportExportStatus
{
    Pending,
    Ready,
    Failed,
    Expired,
}

public sealed class ReportExport : Entity
{
    public Guid TenantId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public string ReportType { get; set; } = string.Empty;

    public string FilterSnapshotJson { get; set; } = "{}";

    public AppLanguage Language { get; set; }

    public string TimeZone { get; set; } = "UTC";

    public ReportExportStatus Status { get; set; } = ReportExportStatus.Pending;

    public string? ContainerName { get; set; }

    public string? BlobName { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
