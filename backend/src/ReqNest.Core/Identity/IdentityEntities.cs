using ReqNest.Core.Common;
using ReqNest.Core.Tenancy;

namespace ReqNest.Core.Identity;

public enum AppLanguage
{
    English,
    French,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public enum MembershipStatus
{
    Invited,
    Active,
    Deactivated,
}

public enum AppRole
{
    TenantAdministrator,
    ProjectManager,
    Contributor,
    Observer,
}

public enum AccountTokenPurpose
{
    VerifyEmail,
    ResetPassword,
}

public sealed class User : Entity
{
    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    public bool IsActive { get; set; } = true;

    public AppLanguage PreferredLanguage { get; set; } = AppLanguage.English;

    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;

    public string? AvatarBlobName { get; set; }

    public string? AvatarContentType { get; set; }

    public DateTimeOffset? LastSignedInAt { get; set; }

    public int FailedSignInCount { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public ICollection<TenantMembership> Memberships { get; set; } = [];
}

public sealed class AccountToken : Entity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public AccountTokenPurpose Purpose { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class TenantMembership : Entity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public MembershipStatus Status { get; set; } = MembershipStatus.Invited;

    public DateTimeOffset? InvitationExpiresAt { get; set; }

    public string? InvitationTokenHash { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public ICollection<RoleGrant> RoleGrants { get; set; } = [];
}

public sealed class RoleGrant : Entity
{
    public Guid TenantId { get; set; }

    public Guid TenantMembershipId { get; set; }

    public TenantMembership TenantMembership { get; set; } = null!;

    public AppRole Role { get; set; }

    public bool AllProjects { get; set; }

    public Guid? GrantedByUserId { get; set; }

    public ICollection<RoleGrantProject> ProjectScopes { get; set; } = [];
}

public sealed class RoleGrantProject
{
    public Guid TenantId { get; set; }

    public Guid RoleGrantId { get; set; }

    public RoleGrant RoleGrant { get; set; } = null!;

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;
}

public sealed class UserSession : Entity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? UserAgent { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
