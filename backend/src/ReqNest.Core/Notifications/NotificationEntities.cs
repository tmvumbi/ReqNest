using ReqNest.Core.Common;
using ReqNest.Core.Identity;

namespace ReqNest.Core.Notifications;

public enum NotificationType
{
    TicketAssigned,
    UserMentioned,
    TicketCommented,
    TicketStatusChanged,
    TicketPriorityChanged,
    DueDateApproaching,
    DueDatePassed,
    SlaAtRisk,
    SlaBreached,
    TicketResolved,
    TicketReopened,
    ProjectMembershipChanged,
    RoleChanged,
    InvitationCreated,
    ReportReady,
    ReportFailed,
}

public sealed class Notification : Entity
{
    public Guid TenantId { get; set; }

    public Guid RecipientUserId { get; set; }

    public User RecipientUser { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public NotificationType Type { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? TicketId { get; set; }

    public string EventKey { get; set; } = string.Empty;

    public string SummaryEnglish { get; set; } = string.Empty;

    public string SummaryFrench { get; set; } = string.Empty;

    public string DeepLink { get; set; } = string.Empty;

    public string? GroupKey { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class NotificationPreference : Entity
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public bool CommentsEnabled { get; set; } = true;

    public bool WatcherUpdatesEnabled { get; set; } = true;

    public bool DueDateUpdatesEnabled { get; set; } = true;

    public bool DigestEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    public int DigestHourLocal { get; set; } = 8;

    public DateTimeOffset? LastDigestAt { get; set; }
}

public enum EmailOutboxStatus
{
    Pending,
    Sent,
    Failed,
}

public sealed class EmailOutboxMessage : Entity
{
    public Guid TenantId { get; set; }

    public Guid RecipientUserId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;

    public string BodyHtml { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string DeduplicationKey { get; set; } = string.Empty;

    public EmailOutboxStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    public string? LastError { get; set; }
}
