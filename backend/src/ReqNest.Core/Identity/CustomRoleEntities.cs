using ReqNest.Core.Common;
using ReqNest.Core.Tenancy;

namespace ReqNest.Core.Identity;

public static class AppPermission
{
    public const string ProjectRead = "project.read";
    public const string ProjectManage = "project.manage";
    public const string WorkflowManage = "workflow.manage";
    public const string TicketMaintain = "ticket.maintain";
    public const string TicketArchive = "ticket.archive";
    public const string TicketBulk = "ticket.bulk";
    public const string CommentAdd = "comment.add";
    public const string AttachmentAdd = "attachment.add";
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";
    public const string UserManage = "user.manage";
    public const string TenantSettingsManage = "tenant.settings.manage";
    public const string AuditView = "audit.view";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ProjectRead,
        ProjectManage,
        WorkflowManage,
        TicketMaintain,
        TicketArchive,
        TicketBulk,
        CommentAdd,
        AttachmentAdd,
        ReportView,
        ReportExport,
        UserManage,
        TenantSettingsManage,
        AuditView,
    };
}

public sealed class CustomRole : Entity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string[] Permissions { get; set; } = [];

    public bool IsActive { get; set; } = true;
}

public sealed class CustomRoleGrant : Entity
{
    public Guid TenantId { get; set; }

    public Guid TenantMembershipId { get; set; }

    public TenantMembership TenantMembership { get; set; } = null!;

    public Guid CustomRoleId { get; set; }

    public CustomRole CustomRole { get; set; } = null!;

    public bool AllProjects { get; set; }

    public Guid? GrantedByUserId { get; set; }

    public ICollection<CustomRoleGrantProject> ProjectScopes { get; set; } = [];
}

public sealed class CustomRoleGrantProject
{
    public Guid TenantId { get; set; }

    public Guid CustomRoleGrantId { get; set; }

    public CustomRoleGrant CustomRoleGrant { get; set; } = null!;

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;
}
