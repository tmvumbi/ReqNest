namespace ReqNest.Core.Identity;

public sealed record RegisterTenantCommand(
    string CompanyName,
    string CompanyShortName,
    string DisplayName,
    string Email,
    string Password,
    AppLanguage Language,
    string TimeZone);

public sealed record LoginCommand(string Email, string Password, string? UserAgent);

public sealed record TokenIssueResult(string? RawToken);

public sealed record AccountActionResult(bool Succeeded, string? ErrorCode)
{
    public static AccountActionResult Success() => new(true, null);

    public static AccountActionResult Failure(string code) => new(false, code);
}

public sealed record TenantAccessSummary(
    Guid TenantId,
    string TenantName,
    string TenantShortName,
    IReadOnlyCollection<AppRole> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> CustomRoles,
    IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> ProjectPermissions);

public sealed record AuthenticatedSession(
    Guid UserId,
    string Email,
    string DisplayName,
    AppLanguage PreferredLanguage,
    ThemePreference ThemePreference,
    string AccessToken,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<TenantAccessSummary> Tenants);

public sealed record AuthenticationResult(AuthenticatedSession? Session, string? ErrorCode)
{
    public bool Succeeded => Session is not null;

    public static AuthenticationResult Success(AuthenticatedSession session) => new(session, null);

    public static AuthenticationResult Failure(string errorCode) => new(null, errorCode);
}

public sealed record AuthenticatedUser(Guid UserId, string Email, string DisplayName);

public interface IAuthenticationService
{
    Task<AuthenticationResult> RegisterTenantAsync(
        RegisterTenantCommand command,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default);

    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TokenIssueResult> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<AccountActionResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<AccountActionResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<AccountActionResult> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public interface ISessionValidationService
{
    Task<AuthenticatedUser?> ValidateAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}

public sealed record TenantAuthorization(
    Guid TenantId,
    Guid MembershipId,
    IReadOnlyCollection<AppRole> AllProjectRoles,
    IReadOnlyDictionary<Guid, IReadOnlyCollection<AppRole>> ProjectRoles,
    IReadOnlyCollection<string> AllProjectPermissions,
    IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> ProjectPermissions)
{
    public bool HasTenantRole(AppRole role) => AllProjectRoles.Contains(role);

    public bool CanAccessProject(Guid projectId) =>
        AllProjectRoles.Count > 0 ||
        ProjectRoles.ContainsKey(projectId) ||
        AllProjectPermissions.Count > 0 ||
        ProjectPermissions.ContainsKey(projectId);

    public bool HasProjectRole(Guid projectId, params AppRole[] roles) =>
        roles.Any(AllProjectRoles.Contains) ||
        (ProjectRoles.TryGetValue(projectId, out var scopedRoles) && roles.Any(scopedRoles.Contains));

    public bool HasPermission(Guid projectId, string permission) =>
        AllProjectPermissions.Contains(permission, StringComparer.Ordinal) ||
        ProjectPermissions.TryGetValue(projectId, out var scopedPermissions) &&
        scopedPermissions.Contains(permission, StringComparer.Ordinal);
}

public interface ITenantAuthorizationService
{
    Task<TenantAuthorization?> GetAuthorizationAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
