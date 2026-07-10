using System.Security.Claims;
using ReqNest.Core.Identity;
using ReqNest.Core.Tenancy;

namespace ReqNest.Api.Authentication;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContext tenantContext,
        ITenantAuthorizationService tenantAuthorizationService)
    {
        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdValue, out var userId))
        {
            tenantContext.UserId = userId;
        }

        var tenantHeader = httpContext.Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenantHeader))
        {
            await next(httpContext);
            return;
        }

        if (tenantContext.UserId is null)
        {
            await next(httpContext);
            return;
        }

        if (!Guid.TryParse(tenantHeader, out var tenantId))
        {
            await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest, "Invalid tenant context");
            return;
        }

        var apiTenantClaim = httpContext.User.FindFirstValue("reqnest:tenant");
        if (apiTenantClaim is not null)
        {
            if (!Guid.TryParse(apiTenantClaim, out var apiTenantId) || apiTenantId != tenantId)
            {
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "Tenant access denied");
                return;
            }

            var scopes = httpContext.User.FindAll("reqnest:scope").Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal).ToArray();
            var projectIds = httpContext.User.FindAll("reqnest:project")
                .Select(claim => Guid.TryParse(claim.Value, out var projectId) ? projectId : Guid.Empty)
                .Where(projectId => projectId != Guid.Empty)
                .Distinct()
                .ToArray();
            var scopedPermissions = projectIds.ToDictionary(
                projectId => projectId,
                _ => (IReadOnlyCollection<string>)scopes);
            var tokenAuthorization = new TenantAuthorization(
                tenantId,
                Guid.Empty,
                [],
                new Dictionary<Guid, IReadOnlyCollection<AppRole>>(),
                projectIds.Length == 0 ? scopes : [],
                scopedPermissions);
            tenantContext.TenantId = tenantId;
            httpContext.Items[nameof(TenantAuthorization)] = tokenAuthorization;
            await next(httpContext);
            return;
        }

        var authorization = await tenantAuthorizationService.GetAuthorizationAsync(
            tenantContext.UserId.Value,
            tenantId,
            httpContext.RequestAborted);

        if (authorization is null)
        {
            await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "Tenant access denied");
            return;
        }

        tenantContext.TenantId = tenantId;
        httpContext.Items[nameof(TenantAuthorization)] = authorization;
        await next(httpContext);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title,
            status = statusCode,
            traceId = context.TraceIdentifier,
        });
    }
}
