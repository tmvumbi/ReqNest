using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using ReqNest.Core.Identity;

namespace ReqNest.Api.Endpoints;

internal static class EndpointSecurity
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static TenantAuthorization? TenantAuthorization(this HttpContext context) =>
        context.Items.TryGetValue(nameof(ReqNest.Core.Identity.TenantAuthorization), out var value)
            ? value as TenantAuthorization
            : null;

    public static bool IsTenantAdministrator(this TenantAuthorization authorization) =>
        authorization.HasTenantRole(AppRole.TenantAdministrator);

    public static bool CanManageProject(this TenantAuthorization authorization, Guid projectId) =>
        authorization.HasProjectRole(projectId, AppRole.TenantAdministrator, AppRole.ProjectManager) ||
        authorization.HasPermission(projectId, AppPermission.ProjectManage);

    public static bool CanMaintainTickets(this TenantAuthorization authorization, Guid projectId) =>
        authorization.HasProjectRole(
            projectId,
            AppRole.TenantAdministrator,
            AppRole.ProjectManager,
            AppRole.Contributor) ||
        authorization.HasPermission(projectId, AppPermission.TicketMaintain);

    public static bool CanViewReports(this TenantAuthorization authorization, Guid projectId) =>
        authorization.CanAccessProject(projectId) &&
        (authorization.HasPermission(projectId, AppPermission.ReportView) ||
         authorization.HasProjectRole(
             projectId,
             AppRole.TenantAdministrator,
             AppRole.ProjectManager,
             AppRole.Contributor,
             AppRole.Observer));

    public static bool CanExportReports(this TenantAuthorization authorization, Guid projectId) =>
        authorization.HasPermission(projectId, AppPermission.ReportExport) ||
        authorization.HasProjectRole(projectId, AppRole.TenantAdministrator, AppRole.ProjectManager);
}

internal static class ApiProblems
{
    public static ProblemHttpResult TenantRequired(HttpContext context) => Problem(
        context,
        StatusCodes.Status400BadRequest,
        "tenant_required",
        "An active company is required.");

    public static ProblemHttpResult Forbidden(HttpContext context) => Problem(
        context,
        StatusCodes.Status403Forbidden,
        "forbidden",
        "You are not permitted to perform this action.");

    public static ProblemHttpResult NotFound(HttpContext context, string resource = "Resource") => Problem(
        context,
        StatusCodes.Status404NotFound,
        "not_found",
        $"{resource} was not found.");

    public static ProblemHttpResult Validation(HttpContext context, string detail, string code = "validation_error") =>
        Problem(context, StatusCodes.Status400BadRequest, code, detail);

    public static ProblemHttpResult Conflict(HttpContext context, string detail, string code = "conflict") =>
        Problem(context, StatusCodes.Status409Conflict, code, detail);

    private static ProblemHttpResult Problem(
        HttpContext context,
        int statusCode,
        string code,
        string detail) => TypedResults.Problem(
            statusCode: statusCode,
            title: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier,
            });
}
