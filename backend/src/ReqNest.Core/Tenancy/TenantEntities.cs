using ReqNest.Core.Common;
using ReqNest.Core.Identity;
using ReqNest.Core.Workflows;

namespace ReqNest.Core.Tenancy;

public sealed class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public AppLanguage DefaultLanguage { get; set; } = AppLanguage.English;

    public string TimeZone { get; set; } = "UTC";

    public ThemePreference DefaultTheme { get; set; } = ThemePreference.System;

    public string PrimaryColor { get; set; } = "#4f46e5";

    public string? LogoBlobName { get; set; }

    public string? LogoContentType { get; set; }

    public string? DarkLogoBlobName { get; set; }

    public string? DarkLogoContentType { get; set; }

    public string? SupportContact { get; set; }

    public string? ReportFooterText { get; set; }

    public ICollection<TenantMembership> Memberships { get; set; } = [];

    public ICollection<Project> Projects { get; set; } = [];

    public ICollection<Workflow> Workflows { get; set; } = [];
}

public sealed class Project : Entity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string Key { get; set; } = string.Empty;

    public string NameEnglish { get; set; } = string.Empty;

    public string NameFrench { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsArchived { get; set; }

    public Guid WorkflowId { get; set; }

    public Workflow Workflow { get; set; } = null!;

    public long NextTicketNumber { get; set; } = 1;

    public Guid? DefaultAssigneeUserId { get; set; }

    public int DefaultPriority { get; set; } = 1;

    public int? FirstResponseTargetMinutes { get; set; }

    public int? ResolutionTargetMinutes { get; set; }
}
