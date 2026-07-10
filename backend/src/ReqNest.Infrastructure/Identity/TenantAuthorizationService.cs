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

        return new TenantAuthorization(
            tenantId,
            membership.Id,
            allProjectRoles,
            projectRoles);
    }
}
