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
}
