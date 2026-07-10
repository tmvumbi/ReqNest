using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class CustomRoleEndpoints
{
    public static IEndpointRouteBuilder MapCustomRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var roles = endpoints.MapGroup("/api/custom-roles")
            .RequireAuthorization()
            .WithTags("Custom roles");
        roles.MapGet("/", ListAsync);
        roles.MapPost("/", CreateAsync);
        roles.MapPut("/{roleId:guid}", UpdateAsync);

        endpoints.MapGet("/api/members/{membershipId:guid}/custom-role-grants", ListGrantsAsync)
            .RequireAuthorization()
            .WithTags("Custom roles");
        endpoints.MapPut("/api/members/{membershipId:guid}/custom-role-grants", UpdateGrantsAsync)
            .RequireAuthorization()
            .WithTags("Custom roles");
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

        if (!authorization.IsTenantAdministrator())
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var roles = await dbContext.CustomRoles.AsNoTracking()
            .OrderBy(entity => entity.Name)
            .Select(entity => new CustomRoleResponse(
                entity.Id,
                entity.Name,
                entity.Description,
                entity.Permissions,
                entity.IsActive,
                dbContext.CustomRoleGrants.Count(grant => grant.CustomRoleId == entity.Id)))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<CustomRoleResponse>>(roles);
    }

    private static async Task<IResult> CreateAsync(
        UpsertCustomRoleRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = Validate(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var role = new CustomRole { TenantId = authorization!.TenantId };
        Apply(role, request);
        dbContext.CustomRoles.Add(role);
        AddAudit(dbContext, httpContext, authorization.TenantId, "custom_role.created", role.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/custom-roles/{role.Id}", ToResponse(role, 0));
    }

    private static async Task<IResult> UpdateAsync(
        Guid roleId,
        UpsertCustomRoleRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = Validate(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var role = await dbContext.CustomRoles.SingleOrDefaultAsync(entity => entity.Id == roleId, cancellationToken);
        if (role is null)
        {
            return ApiProblems.NotFound(httpContext, "Custom role");
        }

        Apply(role, request);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "custom_role.updated", role.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        var grantCount = await dbContext.CustomRoleGrants.CountAsync(entity => entity.CustomRoleId == role.Id, cancellationToken);
        return TypedResults.Ok(ToResponse(role, grantCount));
    }

    private static async Task<IResult> ListGrantsAsync(
        Guid membershipId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.IsTenantAdministrator() && authorization.MembershipId != membershipId)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var grants = await dbContext.CustomRoleGrants.AsNoTracking()
            .Where(entity => entity.TenantMembershipId == membershipId)
            .OrderBy(entity => entity.CustomRole.Name)
            .Select(entity => new CustomRoleGrantResponse(
                entity.Id,
                entity.CustomRoleId,
                entity.CustomRole.Name,
                entity.AllProjects,
                entity.ProjectScopes.Select(scope => scope.ProjectId).ToArray()))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<CustomRoleGrantResponse>>(grants);
    }

    private static async Task<IResult> UpdateGrantsAsync(
        Guid membershipId,
        UpdateCustomRoleGrantsRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.IsTenantAdministrator())
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var membershipExists = await dbContext.TenantMemberships.AnyAsync(
            entity => entity.Id == membershipId && entity.Status == MembershipStatus.Active,
            cancellationToken);
        if (!membershipExists)
        {
            return ApiProblems.NotFound(httpContext, "Active membership");
        }

        var roleIds = request.Grants.Select(grant => grant.CustomRoleId).Distinct().ToArray();
        var validRoleIds = await dbContext.CustomRoles.AsNoTracking()
            .Where(entity => roleIds.Contains(entity.Id) && entity.IsActive)
            .Select(entity => entity.Id)
            .ToArrayAsync(cancellationToken);
        var projectIds = request.Grants.SelectMany(grant => grant.ProjectIds).Distinct().ToArray();
        var validProjectCount = await dbContext.Projects.AsNoTracking()
            .CountAsync(entity => projectIds.Contains(entity.Id), cancellationToken);
        if (validRoleIds.Length != roleIds.Length || validProjectCount != projectIds.Length ||
            request.Grants.Any(grant => grant.AllProjects == (grant.ProjectIds.Count > 0)))
        {
            return ApiProblems.Validation(httpContext, "Custom role grants contain invalid roles or project scopes.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.CustomRoleGrants.Where(entity => entity.TenantMembershipId == membershipId)
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var grantRequest in request.Grants)
        {
            var grant = new CustomRoleGrant
            {
                TenantId = authorization.TenantId,
                TenantMembershipId = membershipId,
                CustomRoleId = grantRequest.CustomRoleId,
                AllProjects = grantRequest.AllProjects,
                GrantedByUserId = httpContext.User.UserId(),
            };
            grant.ProjectScopes = grantRequest.ProjectIds.Select(projectId => new CustomRoleGrantProject
            {
                TenantId = authorization.TenantId,
                CustomRoleGrantId = grant.Id,
                CustomRoleGrant = grant,
                ProjectId = projectId,
            }).ToArray();
            dbContext.CustomRoleGrants.Add(grant);
        }

        AddAudit(dbContext, httpContext, authorization.TenantId, "custom_role.grants.updated", membershipId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static IResult? Validate(
        UpsertCustomRoleRequest request,
        TenantAuthorization? authorization,
        HttpContext context)
    {
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        if (!authorization.IsTenantAdministrator())
        {
            return ApiProblems.Forbidden(context);
        }

        var permissions = request.Permissions.Distinct(StringComparer.Ordinal).ToArray();
        return string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120 ||
               permissions.Length == 0 || permissions.Any(permission => !AppPermission.All.Contains(permission))
            ? ApiProblems.Validation(context, "A name and valid permission set are required.")
            : null;
    }

    private static void Apply(CustomRole role, UpsertCustomRoleRequest request)
    {
        role.Name = request.Name.Trim();
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        role.Permissions = request.Permissions.Distinct(StringComparer.Ordinal).Order().ToArray();
        role.IsActive = request.IsActive;
    }

    private static CustomRoleResponse ToResponse(CustomRole role, int grantCount) => new(
        role.Id,
        role.Name,
        role.Description,
        role.Permissions,
        role.IsActive,
        grantCount);

    private static void AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Guid tenantId,
        string action,
        Guid targetId) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = nameof(CustomRole),
            TargetId = targetId.ToString(),
            Summary = "Custom role configuration changed.",
            CorrelationId = context.TraceIdentifier,
        });
}

public sealed record UpsertCustomRoleRequest(
    string Name,
    string? Description,
    IReadOnlyCollection<string> Permissions,
    bool IsActive);

public sealed record CustomRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyCollection<string> Permissions,
    bool IsActive,
    int GrantCount);

public sealed record CustomRoleGrantRequest(
    Guid CustomRoleId,
    bool AllProjects,
    IReadOnlyCollection<Guid> ProjectIds);

public sealed record UpdateCustomRoleGrantsRequest(IReadOnlyCollection<CustomRoleGrantRequest> Grants);

public sealed record CustomRoleGrantResponse(
    Guid Id,
    Guid CustomRoleId,
    string RoleName,
    bool AllProjects,
    IReadOnlyCollection<Guid> ProjectIds);
