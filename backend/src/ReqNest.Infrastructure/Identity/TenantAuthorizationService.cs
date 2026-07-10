using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Identity;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Identity;

public sealed class TenantAuthorizationService(ReqNestDbContext dbContext)
    : ITenantAuthorizationService
{
    public async Task<TenantAuthorization?> GetAuthorizationAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var membership = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(candidate =>
                candidate.TenantId == tenantId &&
                candidate.UserId == userId &&
                candidate.Status == MembershipStatus.Active)
            .Include(candidate => candidate.RoleGrants)
            .ThenInclude(grant => grant.ProjectScopes)
            .SingleOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var allProjectRoles = membership.RoleGrants
            .Where(grant => grant.AllProjects)
            .Select(grant => grant.Role)
            .Distinct()
            .ToArray();

        var projectRoles = membership.RoleGrants
            .Where(grant => !grant.AllProjects)
            .SelectMany(grant => grant.ProjectScopes.Select(scope => new { scope.ProjectId, grant.Role }))
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<AppRole>)group.Select(item => item.Role).Distinct().ToArray());

        var customGrants = await dbContext.CustomRoleGrants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(grant =>
                grant.TenantId == tenantId &&
                grant.TenantMembershipId == membership.Id &&
                grant.CustomRole.IsActive)
            .Include(grant => grant.CustomRole)
            .Include(grant => grant.ProjectScopes)
            .ToArrayAsync(cancellationToken);
        var allProjectPermissions = customGrants
            .Where(grant => grant.AllProjects)
            .SelectMany(grant => grant.CustomRole.Permissions)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var projectPermissions = customGrants
            .Where(grant => !grant.AllProjects)
            .SelectMany(grant => grant.ProjectScopes.SelectMany(scope =>
                grant.CustomRole.Permissions.Select(permission => new { scope.ProjectId, Permission = permission })))
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group.Select(item => item.Permission)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());

        return new TenantAuthorization(
            tenantId,
            membership.Id,
            allProjectRoles,
            projectRoles,
            allProjectPermissions,
            projectPermissions);
    }
}
