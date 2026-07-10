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
