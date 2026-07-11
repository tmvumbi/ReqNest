using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Configuration;
using ReqNest.Core.Identity;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Workflows;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Identity;

public sealed class AuthenticationService(
    ReqNestDbContext dbContext,
    IPasswordHasher<User> passwordHasher)
    : IAuthenticationService, ISessionValidationService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private const int MaximumFailedSignIns = 5;

    public async Task<AuthenticationResult> RegisterTenantAsync(
        RegisterTenantCommand command,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(command.Email);

        if (!IsRegistrationValid(command, normalizedEmail))
        {
            return AuthenticationResult.Failure("invalid_registration");
        }

        var userExists = await dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (userExists)
        {
            return AuthenticationResult.Failure("email_already_registered");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            Email = command.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = command.DisplayName.Trim(),
            PreferredLanguage = command.Language,
            EmailVerified = true,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, command.Password);

        var tenant = new Tenant
        {
            Name = command.CompanyName.Trim(),
            ShortName = command.CompanyShortName.Trim(),
            DefaultLanguage = command.Language,
            TimeZone = string.IsNullOrWhiteSpace(command.TimeZone) ? "UTC" : command.TimeZone.Trim(),
        };

        var membership = new TenantMembership
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            UserId = user.Id,
            User = user,
            Status = MembershipStatus.Active,
            AcceptedAt = DateTimeOffset.UtcNow,
        };
        user.Memberships.Add(membership);
        tenant.Memberships.Add(membership);

        var administratorGrant = new RoleGrant
        {
            TenantId = tenant.Id,
            TenantMembershipId = membership.Id,
            TenantMembership = membership,
            Role = AppRole.TenantAdministrator,
            AllProjects = true,
            GrantedByUserId = user.Id,
        };
        membership.RoleGrants.Add(administratorGrant);

        var workflow = CreateDefaultWorkflow(tenant);
        tenant.Workflows.Add(workflow);

        dbContext.Users.Add(user);
        dbContext.Tenants.Add(tenant);
        dbContext.TicketTypeDefinitions.AddRange(CreateDefaultTypes(tenant.Id));
        dbContext.TicketPriorityDefinitions.AddRange(CreateDefaultPriorities(tenant.Id));
        dbContext.SlaPolicies.Add(CreateDefaultSlaPolicy(tenant.Id, tenant.TimeZone));
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenant.Id,
            ActorUserId = user.Id,
            Action = "tenant.created",
            TargetType = nameof(Tenant),
            TargetId = tenant.Id.ToString(),
            Summary = "Tenant and initial administrator created.",
        });

        var session = CreateSession(user, userAgent);
        dbContext.UserSessions.Add(session.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AuthenticationResult.Success(new AuthenticatedSession(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PreferredLanguage,
            user.ThemePreference,
            session.RawToken,
            session.Entity.ExpiresAt,
            [new TenantAccessSummary(
                tenant.Id,
                tenant.Name,
                tenant.ShortName,
                [AppRole.TenantAdministrator],
                [],
                [],
                new Dictionary<Guid, IReadOnlyCollection<string>>())]));
    }

    public async Task<AuthenticationResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(command.Email);
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return AuthenticationResult.Failure("invalid_credentials");
        }

        var now = DateTimeOffset.UtcNow;
        if (user.LockedUntil > now)
        {
            return AuthenticationResult.Failure("invalid_credentials");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedSignInCount++;
            if (user.FailedSignInCount >= MaximumFailedSignIns)
            {
                user.LockedUntil = now.AddMinutes(15);
                user.FailedSignInCount = 0;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Failure("invalid_credentials");
        }

        if (!user.EmailVerified)
        {
            return AuthenticationResult.Failure("email_verification_required");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, command.Password);
        }

        var memberships = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(membership => membership.UserId == user.Id && membership.Status == MembershipStatus.Active)
            .Include(membership => membership.Tenant)
            .Include(membership => membership.RoleGrants)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return AuthenticationResult.Failure("no_active_membership");
        }

        user.LastSignedInAt = now;
        user.FailedSignInCount = 0;
        user.LockedUntil = null;
        var session = CreateSession(user, command.UserAgent);
        dbContext.UserSessions.Add(session.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var membershipIds = memberships.Select(membership => membership.Id).ToArray();
        var customGrants = await dbContext.CustomRoleGrants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(grant => membershipIds.Contains(grant.TenantMembershipId) && grant.CustomRole.IsActive)
            .Include(grant => grant.CustomRole)
            .Include(grant => grant.ProjectScopes)
            .ToArrayAsync(cancellationToken);

        var tenants = memberships
            .Select(membership => new TenantAccessSummary(
                membership.TenantId,
                membership.Tenant.Name,
                membership.Tenant.ShortName,
                membership.RoleGrants.Select(grant => grant.Role).Distinct().ToArray(),
                customGrants.Where(grant => grant.TenantMembershipId == membership.Id && grant.AllProjects)
                    .SelectMany(grant => grant.CustomRole.Permissions)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                customGrants.Where(grant => grant.TenantMembershipId == membership.Id)
                    .Select(grant => grant.CustomRole.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                customGrants.Where(grant => grant.TenantMembershipId == membership.Id && !grant.AllProjects)
                    .SelectMany(grant => grant.ProjectScopes.Select(scope => new
                    {
                        scope.ProjectId,
                        grant.CustomRole.Permissions,
                    }))
                    .GroupBy(item => item.ProjectId)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyCollection<string>)group
                            .SelectMany(item => item.Permissions)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray())))
            .ToArray();

        return AuthenticationResult.Success(new AuthenticatedSession(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PreferredLanguage,
            user.ThemePreference,
            session.RawToken,
            session.Entity.ExpiresAt,
            tenants));
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(accessToken);
        var session = await dbContext.UserSessions
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (session is not null && session.RevokedAt is null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAt, now),
                cancellationToken);
    }

    public async Task<TokenIssueResult> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.NormalizedEmail == normalizedEmail && entity.IsActive, cancellationToken);
        if (user is null)
        {
            return new TokenIssueResult(null);
        }

        var token = CreateAccountToken(user, AccountTokenPurpose.ResetPassword, ResetTokenLifetime);
        dbContext.AccountTokens.Add(token.Entity);
        await AddSecurityAuditsAsync(user.Id, "account.password_reset.requested", "Password reset was requested.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TokenIssueResult(token.RawToken);
    }

    public async Task<AccountActionResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (newPassword.Length < 12)
        {
            return AccountActionResult.Failure("invalid_password");
        }

        var accountToken = await FindUsableTokenAsync(token, AccountTokenPurpose.ResetPassword, cancellationToken);
        if (accountToken is null)
        {
            return AccountActionResult.Failure("invalid_or_expired_token");
        }

        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(entity => entity.Id == accountToken.UserId, cancellationToken);
        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.FailedSignInCount = 0;
        user.LockedUntil = null;
        accountToken.ConsumedAt = DateTimeOffset.UtcNow;
        await RevokeSessionsAsync(user.Id, cancellationToken);
        await AddSecurityAuditsAsync(user.Id, "account.password_reset.completed", "Password was reset.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AccountActionResult.Success();
    }

    public async Task<AccountActionResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (newPassword.Length < 12)
        {
            return AccountActionResult.Failure("invalid_password");
        }

        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(entity => entity.Id == userId, cancellationToken);
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            return AccountActionResult.Failure("invalid_credentials");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        await RevokeSessionsAsync(user.Id, cancellationToken);
        await AddSecurityAuditsAsync(user.Id, "account.password.changed", "Password was changed.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AccountActionResult.Success();
    }

    public async Task<AccountActionResult> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var accountToken = await FindUsableTokenAsync(token, AccountTokenPurpose.VerifyEmail, cancellationToken);
        if (accountToken is null)
        {
            return AccountActionResult.Failure("invalid_or_expired_token");
        }

        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(entity => entity.Id == accountToken.UserId, cancellationToken);
        user.EmailVerified = true;
        accountToken.ConsumedAt = DateTimeOffset.UtcNow;
        await AddSecurityAuditsAsync(user.Id, "account.email.verified", "Email address was verified.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AccountActionResult.Success();
    }

    public async Task<AuthenticatedUser?> ValidateAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(accessToken);
        var session = await dbContext.UserSessions
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (session is null || !session.IsActive(now) || !session.User.IsActive)
        {
            return null;
        }

        session.LastUsedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticatedUser(session.UserId, session.User.Email, session.User.DisplayName);
    }

    private static bool IsRegistrationValid(RegisterTenantCommand command, string normalizedEmail) =>
        !string.IsNullOrWhiteSpace(command.CompanyName) &&
        !string.IsNullOrWhiteSpace(command.CompanyShortName) &&
        command.CompanyShortName.Trim().Length <= 40 &&
        !string.IsNullOrWhiteSpace(command.DisplayName) &&
        normalizedEmail.Contains('@', StringComparison.Ordinal) &&
        command.Password.Length >= 12;

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string HashToken(string accessToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

    internal static string HashOpaqueToken(string token) => HashToken(token);

    internal static (AccountToken Entity, string RawToken) CreateAccountToken(
        User user,
        AccountTokenPurpose purpose,
        TimeSpan lifetime)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (new AccountToken
        {
            UserId = user.Id,
            User = user,
            Purpose = purpose,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime),
        }, rawToken);
    }

    internal static TimeSpan EmailVerificationLifetime => VerificationTokenLifetime;

    private async Task<AccountToken?> FindUsableTokenAsync(
        string rawToken,
        AccountTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = HashToken(rawToken);
        var now = DateTimeOffset.UtcNow;
        return await dbContext.AccountTokens.IgnoreQueryFilters().SingleOrDefaultAsync(entity =>
            entity.TokenHash == hash &&
            entity.Purpose == purpose &&
            entity.ConsumedAt == null &&
            entity.ExpiresAt > now,
            cancellationToken);
    }

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken);

    private async Task AddSecurityAuditsAsync(
        Guid userId,
        string action,
        string summary,
        CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.TenantMemberships.IgnoreQueryFilters()
            .Where(entity => entity.UserId == userId)
            .Select(entity => entity.TenantId)
            .ToArrayAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            dbContext.AuditEvents.Add(new AuditEvent
            {
                TenantId = tenantId,
                ActorUserId = userId,
                Action = action,
                TargetType = nameof(User),
                TargetId = userId.ToString(),
                Summary = summary,
            });
        }
    }

    private static (UserSession Entity, string RawToken) CreateSession(User user, string? userAgent)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (new UserSession
        {
            UserId = user.Id,
            User = user,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 512)],
        }, rawToken);
    }

    private static Workflow CreateDefaultWorkflow(Tenant tenant)
    {
        var workflow = new Workflow
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            Name = "Default",
            Description = "TODO → IN PROGRESS → DONE",
            IsDefault = true,
        };

        var todo = new WorkflowStatus
        {
            TenantId = tenant.Id,
            WorkflowId = workflow.Id,
            Workflow = workflow,
            Key = "TODO",
            Label = "TODO",
            Category = WorkflowStatusCategory.ToDo,
            Order = 1,
            Color = "#64748b",
            IsInitial = true,
        };
        var inProgress = new WorkflowStatus
        {
            TenantId = tenant.Id,
            WorkflowId = workflow.Id,
            Workflow = workflow,
            Key = "IN_PROGRESS",
            Label = "IN PROGRESS",
            Category = WorkflowStatusCategory.InProgress,
            Order = 2,
            Color = "#2563eb",
        };
        var done = new WorkflowStatus
        {
            TenantId = tenant.Id,
            WorkflowId = workflow.Id,
            Workflow = workflow,
            Key = "DONE",
            Label = "DONE",
            Category = WorkflowStatusCategory.Done,
            Order = 3,
            Color = "#16a34a",
            IsTerminal = true,
        };

        workflow.Statuses = [todo, inProgress, done];
        workflow.Transitions =
        [
            CreateTransition(tenant.Id, workflow, todo, inProgress),
            CreateTransition(tenant.Id, workflow, inProgress, todo),
            CreateTransition(tenant.Id, workflow, inProgress, done),
            CreateTransition(tenant.Id, workflow, done, inProgress),
        ];

        return workflow;
    }

    private static IReadOnlyCollection<TicketTypeDefinition> CreateDefaultTypes(Guid tenantId) =>
    [
        new() { TenantId = tenantId, Key = "Incident", Label = "Incident", Order = 1 },
        new() { TenantId = tenantId, Key = "ServiceRequest", Label = "Service request", Order = 2 },
        new() { TenantId = tenantId, Key = "Task", Label = "Task", Order = 3 },
        new() { TenantId = tenantId, Key = "Problem", Label = "Problem", Order = 4 },
    ];

    private static IReadOnlyCollection<TicketPriorityDefinition> CreateDefaultPriorities(Guid tenantId) =>
    [
        new() { TenantId = tenantId, Key = "Low", Label = "Low", Color = "#64748b", Weight = 1, Order = 1 },
        new() { TenantId = tenantId, Key = "Normal", Label = "Normal", Color = "#2563eb", Weight = 2, Order = 2 },
        new() { TenantId = tenantId, Key = "High", Label = "High", Color = "#d97706", Weight = 3, Order = 3 },
        new() { TenantId = tenantId, Key = "Urgent", Label = "Urgent", Color = "#dc2626", Weight = 4, Order = 4 },
    ];

    private static SlaPolicy CreateDefaultSlaPolicy(Guid tenantId, string timeZone)
    {
        var policy = new SlaPolicy
        {
            TenantId = tenantId,
            Name = "Standard business hours",
            TimeZone = timeZone,
            IsDefault = true,
            WarningMinutesBefore = 60,
        };
        policy.Targets =
        [
            new() { TenantId = tenantId, SlaPolicy = policy, SlaPolicyId = policy.Id, PriorityKey = "Low", FirstResponseMinutes = 480, ResolutionMinutes = 2400 },
            new() { TenantId = tenantId, SlaPolicy = policy, SlaPolicyId = policy.Id, PriorityKey = "Normal", FirstResponseMinutes = 240, ResolutionMinutes = 1440 },
            new() { TenantId = tenantId, SlaPolicy = policy, SlaPolicyId = policy.Id, PriorityKey = "High", FirstResponseMinutes = 120, ResolutionMinutes = 480 },
            new() { TenantId = tenantId, SlaPolicy = policy, SlaPolicyId = policy.Id, PriorityKey = "Urgent", FirstResponseMinutes = 30, ResolutionMinutes = 240 },
        ];
        return policy;
    }

    private static WorkflowTransition CreateTransition(
        Guid tenantId,
        Workflow workflow,
        WorkflowStatus from,
        WorkflowStatus to) => new()
        {
            TenantId = tenantId,
            WorkflowId = workflow.Id,
            Workflow = workflow,
            FromStatusId = from.Id,
            FromStatus = from,
            ToStatusId = to.Id,
            ToStatus = to,
        };
}
