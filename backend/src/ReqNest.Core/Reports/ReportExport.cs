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

public enum ReportScheduleFrequency
{
    Daily,
    Weekly,
    Monthly,
}

public enum ReportExportFormat
{
    Pdf,
    Csv,
}

public sealed class ReportSchedule : Entity
{
    public Guid TenantId { get; set; }

    public Guid OwnerUserId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ReportType { get; set; } = string.Empty;

    public string FilterSnapshotJson { get; set; } = "{}";

    public AppLanguage Language { get; set; }

    public ReportExportFormat Format { get; set; }

    public ReportScheduleFrequency Frequency { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset NextRunAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }
}
