using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Identity;
using ReqNest.Core.Auditing;
using ReqNest.Core.Tenancy;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tenants = endpoints.MapGroup("/api/tenants")
            .RequireAuthorization()
            .WithTags("Tenants");
        tenants.MapGet("/", ListAsync);
        tenants.MapGet("/current", GetCurrentAsync);
        tenants.MapPatch("/current", UpdateCurrentAsync);

        endpoints.MapPatch("/api/profile/preferences", UpdatePreferencesAsync)
            .RequireAuthorization()
            .WithTags("Profile");
        return endpoints;
    }

    private static async Task<Ok<IReadOnlyCollection<TenantSummaryResponse>>> ListAsync(
        ClaimsPrincipal principal,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.UserId();
        var tenants = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.UserId == userId && entity.Status == MembershipStatus.Active)
            .OrderBy(entity => entity.Tenant.Name)
            .Select(entity => new TenantSummaryResponse(
                entity.TenantId,
                entity.Tenant.Name,
                entity.Tenant.ShortName,
                entity.Tenant.DefaultLanguage,
                entity.Tenant.DefaultTheme,
                entity.RoleGrants.Select(grant => grant.Role).Distinct().ToArray()))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<TenantSummaryResponse>>(tenants);
    }

    private static async Task<Results<Ok<TenantSettingsResponse>, ProblemHttpResult>> GetCurrentAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(tenant));
    }

    private static async Task<Results<Ok<TenantSettingsResponse>, ProblemHttpResult>> UpdateCurrentAsync(
        UpdateTenantSettingsRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ShortName) ||
            request.ShortName.Trim().Length > 40 || !IsAccessibleColor(request.PrimaryColor))
        {
            return ApiProblems.Validation(httpContext, "Company name, short name, and an accessible accent color are required.");
        }

        var tenant = await dbContext.Tenants.SingleAsync(cancellationToken);
        tenant.Name = request.Name.Trim();
        tenant.ShortName = request.ShortName.Trim();
        tenant.DefaultLanguage = request.DefaultLanguage;
        tenant.DefaultTheme = request.DefaultTheme;
        tenant.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone.Trim();
        tenant.PrimaryColor = request.PrimaryColor;
        tenant.SupportContact = request.SupportContact?.Trim();
        tenant.ReportFooterText = request.ReportFooterText?.Trim();
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenant.Id,
            ActorUserId = httpContext.User.UserId(),
            Action = "tenant.settings.updated",
            TargetType = nameof(Tenant),
            TargetId = tenant.Id.ToString(),
            Summary = "Company settings were updated.",
            CorrelationId = httpContext.TraceIdentifier,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(tenant));
    }

    private static async Task<Results<Ok<UserPreferencesResponse>, ProblemHttpResult>> UpdatePreferencesAsync(
        UpdateUserPreferencesRequest request,
        ClaimsPrincipal principal,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.UserId();
        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(entity => entity.Id == userId, cancellationToken);
        user.PreferredLanguage = request.Language;
        user.ThemePreference = request.Theme;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new UserPreferencesResponse(user.PreferredLanguage, user.ThemePreference));
    }

    private static bool IsAccessibleColor(string value) =>
        value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static TenantSettingsResponse ToResponse(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.ShortName,
        tenant.DefaultLanguage,
        tenant.TimeZone,
        tenant.DefaultTheme,
        tenant.PrimaryColor,
        tenant.LogoBlobName is not null,
        tenant.DarkLogoBlobName is not null,
        tenant.SupportContact,
        tenant.ReportFooterText);
}

public sealed record TenantSummaryResponse(
    Guid Id,
    string Name,
    string ShortName,
    AppLanguage DefaultLanguage,
    ThemePreference DefaultTheme,
    IReadOnlyCollection<AppRole> Roles);

public sealed record TenantSettingsResponse(
    Guid Id,
    string Name,
    string ShortName,
    AppLanguage DefaultLanguage,
    string TimeZone,
    ThemePreference DefaultTheme,
    string PrimaryColor,
    bool HasLogo,
    bool HasDarkLogo,
    string? SupportContact,
    string? ReportFooterText);

public sealed record UpdateTenantSettingsRequest(
    string Name,
    string ShortName,
    AppLanguage DefaultLanguage,
    string TimeZone,
    ThemePreference DefaultTheme,
    string PrimaryColor,
    string? SupportContact,
    string? ReportFooterText);

public sealed record UpdateUserPreferencesRequest(AppLanguage Language, ThemePreference Theme);

public sealed record UserPreferencesResponse(AppLanguage Language, ThemePreference Theme);
