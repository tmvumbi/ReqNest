using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Notifications;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class MemberEndpoints
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/members")
            .RequireAuthorization()
            .WithTags("Members and roles");
        group.MapGet("/", ListAsync);
        group.MapPost("/invitations", InviteAsync);
        group.MapPost("/{membershipId:guid}/resend", ResendAsync);
        group.MapPost("/{membershipId:guid}/revoke", RevokeAsync);
        group.MapPut("/{membershipId:guid}/roles", UpdateRolesAsync);
        group.MapPost("/{membershipId:guid}/deactivate", DeactivateAsync);
        group.MapPost("/{membershipId:guid}/activate", ActivateAsync);

        endpoints.MapPost("/api/auth/accept-invitation", AcceptInvitationAsync)
            .AllowAnonymous()
            .WithTags("Authentication");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var managedProjectIds = authorization.ProjectRoles
            .Where(entry => entry.Value.Contains(AppRole.ProjectManager))
            .Select(entry => entry.Key)
            .ToArray();
        var canSeeAll = authorization.IsTenantAdministrator() || authorization.HasTenantRole(AppRole.ProjectManager);
        if (!canSeeAll && managedProjectIds.Length == 0)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var memberships = await dbContext.TenantMemberships.AsNoTracking()
            .Where(entity => canSeeAll || entity.RoleGrants.Any(grant =>
                !grant.AllProjects && grant.ProjectScopes.Any(scope => managedProjectIds.Contains(scope.ProjectId))))
            .OrderBy(entity => entity.User.DisplayName)
            .Select(entity => new MemberResponse(
                entity.Id,
                entity.UserId,
                entity.User.Email,
                entity.User.DisplayName,
                entity.Status,
                entity.InvitationExpiresAt,
                entity.RoleGrants.Select(grant => new RoleGrantResponse(
                    grant.Id,
                    grant.Role,
                    grant.AllProjects,
                    grant.ProjectScopes.Select(scope => scope.ProjectId).ToArray())).ToArray()))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<MemberResponse>>(memberships);
    }

    private static async Task<IResult> InviteAsync(
        InviteMemberRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        INotificationService notificationService,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var validation = await ValidateGrantsAsync(request.Grants, authorization, dbContext, cancellationToken);
        if (validation is not null)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (!normalizedEmail.Contains('@', StringComparison.Ordinal) || request.Grants.Count == 0)
        {
            return ApiProblems.Validation(httpContext, "A valid email address and at least one role grant are required.");
        }

        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Email.Trim() : request.DisplayName.Trim(),
                EmailVerified = false,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            dbContext.Users.Add(user);
        }

        if (await dbContext.TenantMemberships.IgnoreQueryFilters()
            .AnyAsync(entity => entity.TenantId == authorization.TenantId && entity.UserId == user.Id, cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "This user already has a company membership.", "membership_exists");
        }

        var token = NewToken();
        var membership = new TenantMembership
        {
            TenantId = authorization.TenantId,
            UserId = user.Id,
            User = user,
            Status = MembershipStatus.Invited,
            InvitationTokenHash = HashToken(token),
            InvitationExpiresAt = DateTimeOffset.UtcNow.Add(InvitationLifetime),
        };
        ApplyGrants(membership, request.Grants, httpContext.User.UserId());
        dbContext.TenantMemberships.Add(membership);
        var audit = AddAudit(dbContext, httpContext, membership, "member.invited", "A user was invited.");
        await notificationService.AddAsync(new NotificationMessage(
            authorization.TenantId,
            [user.Id],
            httpContext.User.UserId(),
            NotificationType.InvitationCreated,
            null,
            null,
            audit.Id.ToString(),
            "You were invited to a company in ReqNest.",
            "/accept-invitation",
            membership.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/members/{membership.Id}",
            new InvitationCreatedResponse(membership.Id, membership.InvitationExpiresAt.Value, environment.IsDevelopment() ? token : null));
    }

    private static async Task<IResult> ResendAsync(
        Guid membershipId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        var membership = await dbContext.TenantMemberships.SingleOrDefaultAsync(
            entity => entity.Id == membershipId && entity.Status == MembershipStatus.Invited,
            cancellationToken);
        if (membership is null)
        {
            return ApiProblems.NotFound(httpContext, "Invitation");
        }

        var token = NewToken();
        membership.InvitationTokenHash = HashToken(token);
        membership.InvitationExpiresAt = DateTimeOffset.UtcNow.Add(InvitationLifetime);
        AddAudit(dbContext, httpContext, membership, "member.invitation.resent", "An invitation was resent.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new InvitationCreatedResponse(
            membership.Id,
            membership.InvitationExpiresAt.Value,
            environment.IsDevelopment() ? token : null));
    }

    private static async Task<IResult> RevokeAsync(
        Guid membershipId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        var membership = await dbContext.TenantMemberships.SingleOrDefaultAsync(
            entity => entity.Id == membershipId && entity.Status == MembershipStatus.Invited,
            cancellationToken);
        if (membership is null)
        {
            return ApiProblems.NotFound(httpContext, "Invitation");
        }

        membership.Status = MembershipStatus.Deactivated;
        membership.InvitationTokenHash = null;
        membership.InvitationExpiresAt = null;
        AddAudit(dbContext, httpContext, membership, "member.invitation.revoked", "An invitation was revoked.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        ReqNestDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;
        var membership = await dbContext.TenantMemberships.IgnoreQueryFilters()
            .Include(entity => entity.User)
            .SingleOrDefaultAsync(entity =>
                entity.InvitationTokenHash == tokenHash &&
                entity.Status == MembershipStatus.Invited &&
                entity.InvitationExpiresAt > now,
                cancellationToken);
        if (membership is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The invitation is invalid or has expired.",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_or_expired_invitation" });
        }

        if (!membership.User.EmailVerified)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "A password of at least 12 characters is required.",
                    extensions: new Dictionary<string, object?> { ["code"] = "invalid_password" });
            }

            membership.User.PasswordHash = passwordHasher.HashPassword(membership.User, request.Password);
            membership.User.EmailVerified = true;
            membership.User.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? membership.User.DisplayName
                : request.DisplayName.Trim();
        }

        membership.Status = MembershipStatus.Active;
        membership.AcceptedAt = now;
        membership.InvitationTokenHash = null;
        membership.InvitationExpiresAt = null;
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = membership.TenantId,
            ActorUserId = membership.UserId,
            Action = "member.invitation.accepted",
            TargetType = nameof(TenantMembership),
            TargetId = membership.Id.ToString(),
            Summary = "An invitation was accepted.",
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> UpdateRolesAsync(
        Guid membershipId,
        UpdateMemberRolesRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (await ValidateGrantsAsync(request.Grants, authorization, dbContext, cancellationToken) is not null)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var membership = await dbContext.TenantMemberships
            .Include(entity => entity.RoleGrants)
            .ThenInclude(entity => entity.ProjectScopes)
            .SingleOrDefaultAsync(entity => entity.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return ApiProblems.NotFound(httpContext, "Member");
        }

        var removesAdministrator = membership.RoleGrants.Any(entity => entity.Role == AppRole.TenantAdministrator) &&
                                   !request.Grants.Any(entity => entity.Role == AppRole.TenantAdministrator);
        if (removesAdministrator && await IsLastAdministratorAsync(membership.Id, dbContext, cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "The last active tenant administrator cannot be demoted.", "last_administrator");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.RoleGrants.RemoveRange(membership.RoleGrants);
        await dbContext.SaveChangesAsync(cancellationToken);
        membership.RoleGrants.Clear();
        ApplyGrants(membership, request.Grants, httpContext.User.UserId());
        dbContext.RoleGrants.AddRange(membership.RoleGrants);
        var audit = AddAudit(dbContext, httpContext, membership, "member.roles.updated", "Member roles and project scopes were updated.");
        await notificationService.AddAsync(new NotificationMessage(
            membership.TenantId,
            [membership.UserId],
            httpContext.User.UserId(),
            NotificationType.RoleChanged,
            null,
            null,
            audit.Id.ToString(),
            "Your roles or project access changed.",
            "/settings/profile",
            membership.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static Task<IResult> DeactivateAsync(
        Guid membershipId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetMembershipStatusAsync(membershipId, MembershipStatus.Deactivated, httpContext, dbContext, cancellationToken);

    private static Task<IResult> ActivateAsync(
        Guid membershipId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetMembershipStatusAsync(membershipId, MembershipStatus.Active, httpContext, dbContext, cancellationToken);

    private static async Task<IResult> SetMembershipStatusAsync(
        Guid membershipId,
        MembershipStatus status,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        var membership = await dbContext.TenantMemberships
            .Include(entity => entity.RoleGrants)
            .SingleOrDefaultAsync(entity => entity.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return ApiProblems.NotFound(httpContext, "Member");
        }

        if (status == MembershipStatus.Deactivated &&
            membership.RoleGrants.Any(entity => entity.Role == AppRole.TenantAdministrator) &&
            await IsLastAdministratorAsync(membership.Id, dbContext, cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "The last active tenant administrator cannot be deactivated.", "last_administrator");
        }

        membership.Status = status;
        AddAudit(
            dbContext,
            httpContext,
            membership,
            status == MembershipStatus.Active ? "member.activated" : "member.deactivated",
            status == MembershipStatus.Active ? "A member was activated." : "A member was deactivated.");
        if (status == MembershipStatus.Deactivated)
        {
            await dbContext.UserSessions.Where(entity => entity.UserId == membership.UserId && entity.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(entity => entity.RevokedAt, DateTimeOffset.UtcNow),
                    cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<string?> ValidateGrantsAsync(
        IReadOnlyCollection<RoleGrantRequest> grants,
        TenantAuthorization authorization,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (grants.Count == 0)
        {
            return "no_grants";
        }

        if (authorization.IsTenantAdministrator())
        {
            var requestedIds = grants.SelectMany(entity => entity.ProjectIds).Distinct().ToArray();
            var existingCount = await dbContext.Projects.CountAsync(entity => requestedIds.Contains(entity.Id), cancellationToken);
            return existingCount == requestedIds.Length ? null : "invalid_projects";
        }

        if (grants.Any(entity => entity.AllProjects || entity.Role == AppRole.TenantAdministrator))
        {
            return "forbidden_grant";
        }

        return grants.SelectMany(entity => entity.ProjectIds).All(authorization.CanManageProject)
            ? null
            : "forbidden_projects";
    }

    private static void ApplyGrants(
        TenantMembership membership,
        IReadOnlyCollection<RoleGrantRequest> requests,
        Guid grantedByUserId)
    {
        foreach (var request in requests)
        {
            var grant = new RoleGrant
            {
                TenantId = membership.TenantId,
                TenantMembershipId = membership.Id,
                TenantMembership = membership,
                Role = request.Role,
                AllProjects = request.AllProjects,
                GrantedByUserId = grantedByUserId,
            };
            if (!request.AllProjects)
            {
                grant.ProjectScopes = request.ProjectIds.Distinct().Select(projectId => new RoleGrantProject
                {
                    TenantId = membership.TenantId,
                    RoleGrantId = grant.Id,
                    RoleGrant = grant,
                    ProjectId = projectId,
                }).ToArray();
            }

            membership.RoleGrants.Add(grant);
        }
    }

    private static async Task<bool> IsLastAdministratorAsync(
        Guid excludingMembershipId,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        !await dbContext.TenantMemberships.AnyAsync(entity =>
            entity.Id != excludingMembershipId &&
            entity.Status == MembershipStatus.Active &&
            entity.RoleGrants.Any(grant => grant.Role == AppRole.TenantAdministrator),
            cancellationToken);

    private static AuditEvent AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        TenantMembership membership,
        string action,
        string summary)
    {
        var audit = new AuditEvent
        {
            TenantId = membership.TenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = nameof(TenantMembership),
            TargetId = membership.Id.ToString(),
            Summary = summary,
            CorrelationId = context.TraceIdentifier,
        };
        dbContext.AuditEvents.Add(audit);
        return audit;
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record RoleGrantRequest(AppRole Role, bool AllProjects, IReadOnlyCollection<Guid> ProjectIds);

public sealed record InviteMemberRequest(
    string Email,
    string? DisplayName,
    IReadOnlyCollection<RoleGrantRequest> Grants);

public sealed record InvitationCreatedResponse(Guid MembershipId, DateTimeOffset ExpiresAt, string? DevelopmentToken);

public sealed record AcceptInvitationRequest(string Token, string? DisplayName, string? Password);

public sealed record UpdateMemberRolesRequest(IReadOnlyCollection<RoleGrantRequest> Grants);

public sealed record RoleGrantResponse(
    Guid Id,
    AppRole Role,
    bool AllProjects,
    IReadOnlyCollection<Guid> ProjectIds);

public sealed record MemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string DisplayName,
    MembershipStatus Status,
    DateTimeOffset? InvitationExpiresAt,
    IReadOnlyCollection<RoleGrantResponse> Grants);
