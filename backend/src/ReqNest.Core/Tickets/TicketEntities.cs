using ReqNest.Core.Common;
using ReqNest.Core.Identity;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Workflows;

namespace ReqNest.Core.Tickets;

public enum TicketType
{
    Incident,
    ServiceRequest,
    Task,
    Problem,
}

public enum TicketPriority
{
    Low,
    Normal,
    High,
    Urgent,
}

public enum TicketRelationshipType
{
    RelatesTo,
    Duplicates,
    Blocks,
}

public enum AttachmentScanStatus
{
    Pending,
    Clean,
    Quarantined,
    Failed,
}

public enum SlaState
{
    None,
    OnTrack,
    AtRisk,
    Breached,
    Met,
}

public sealed class Ticket : Entity
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public long Number { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? CreationKey { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DescriptionPlainText { get; set; } = string.Empty;

    public TicketType Type { get; set; } = TicketType.Incident;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public Guid WorkflowStatusId { get; set; }

    public WorkflowStatus WorkflowStatus { get; set; } = null!;

    public Guid ReporterUserId { get; set; }

    public User ReporterUser { get; set; } = null!;

    public Guid? AssigneeUserId { get; set; }

    public User? AssigneeUser { get; set; }

    public string[] Labels { get; set; } = [];

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? FirstRespondedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? FirstResponseTargetAt { get; set; }

    public DateTimeOffset? ResolutionTargetAt { get; set; }

    public SlaState SlaState { get; set; }

    public string? ResolutionSummary { get; set; }

    public bool IsArchived { get; set; }

    public uint Version { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = [];

    public ICollection<TicketWatcher> Watchers { get; set; } = [];

    public ICollection<TicketStatusHistory> StatusHistory { get; set; } = [];

    public ICollection<Attachment> Attachments { get; set; } = [];
}

public sealed class TicketComment : Entity
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public Guid AuthorUserId { get; set; }

    public User AuthorUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public string BodyPlainText { get; set; } = string.Empty;

    public bool IsHidden { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public ICollection<Attachment> Attachments { get; set; } = [];

    public ICollection<TicketCommentRevision> Revisions { get; set; } = [];
}

public sealed class TicketCommentRevision : Entity
{
    public Guid TenantId { get; set; }

    public Guid TicketCommentId { get; set; }

    public TicketComment TicketComment { get; set; } = null!;

    public Guid EditedByUserId { get; set; }

    public string PreviousBody { get; set; } = string.Empty;
}

public sealed class TicketWatcher
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsMuted { get; set; }
}

public sealed class TicketStatusHistory : Entity
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public Guid? FromStatusId { get; set; }

    public Guid ToStatusId { get; set; }

    public Guid ChangedByUserId { get; set; }

    public string? Comment { get; set; }
}

public sealed class TicketRelationship : Entity
{
    public Guid TenantId { get; set; }

    public Guid SourceTicketId { get; set; }

    public Guid TargetTicketId { get; set; }

    public TicketRelationshipType Type { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed class Attachment : Entity
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public Guid? TicketCommentId { get; set; }

    public TicketComment? TicketComment { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string ContainerName { get; set; } = string.Empty;

    public string BlobName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string ChecksumSha256 { get; set; } = string.Empty;

    public AttachmentScanStatus ScanStatus { get; set; } = AttachmentScanStatus.Pending;

    public DateTimeOffset? DeletedAt { get; set; }
}
