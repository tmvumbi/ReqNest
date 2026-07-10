using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Core.Tenancy;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class SsoAuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapSsoAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth/sso").AllowAnonymous().RequireRateLimiting("authentication").WithTags("SSO authentication");
        group.MapGet("/{tenantId:guid}/start", StartAsync);
        group.MapGet("/callback", CallbackAsync);
        group.MapPost("/exchange", ExchangeAsync);
        return endpoints;
    }

    private static async Task<IResult> StartAsync(
        Guid tenantId,
        string? returnUrl,
        HttpContext context,
        ReqNestDbContext dbContext,
        IHttpClientFactory clients,
        IDataProtectionProvider protectionProvider,
        CancellationToken cancellationToken)
    {
        var configuration = await dbContext.TenantSsoConfigurations.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.IsEnabled, cancellationToken);
        if (configuration is null) return Results.NotFound();
        var discovery = await DiscoverAsync(configuration.Authority, clients, cancellationToken);
        if (discovery is null) return ApiProblems.Conflict(context, "OIDC discovery failed.", "oidc_discovery_failed");
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var callback = $"{context.Request.Scheme}://{context.Request.Host}/api/auth/sso/callback";
        var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out _) && returnUrl.StartsWith('/')
            ? returnUrl : "/auth/sso";
        var statePayload = JsonSerializer.Serialize(new SsoState(tenantId, verifier, callback, safeReturnUrl, DateTimeOffset.UtcNow.AddMinutes(10)));
        var state = protectionProvider.CreateProtector("ReqNest.OIDC.State.v1").Protect(statePayload);
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = configuration.ClientId,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["redirect_uri"] = callback,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };
        var query = string.Join('&', parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        return TypedResults.Ok(new SsoStartResponse($"{discovery.AuthorizationEndpoint}?{query}"));
    }

    private static async Task<IResult> CallbackAsync(
        string? code,
        string? state,
        string? error,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        IHttpClientFactory clients,
        IDataProtectionProvider protectionProvider,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Results.Redirect("/auth/sso?error=provider_denied");
        SsoState payload;
        try
        {
            payload = JsonSerializer.Deserialize<SsoState>(protectionProvider.CreateProtector("ReqNest.OIDC.State.v1").Unprotect(state))!;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return Results.Redirect("/auth/sso?error=invalid_state");
        }
        if (payload.ExpiresAt <= DateTimeOffset.UtcNow) return Results.Redirect("/auth/sso?error=expired_state");
        var configuration = await dbContext.TenantSsoConfigurations.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == payload.TenantId && item.IsEnabled, cancellationToken);
        if (configuration is null) return Results.Redirect("/auth/sso?error=configuration_missing");
        var discovery = await DiscoverAsync(configuration.Authority, clients, cancellationToken);
        if (discovery is null) return Results.Redirect("/auth/sso?error=discovery_failed");
        string clientSecret;
        try { clientSecret = protectionProvider.CreateProtector("ReqNest.Oidc.v1").Unprotect(configuration.ProtectedClientSecret); }
        catch (CryptographicException) { return Results.Redirect("/auth/sso?error=configuration_invalid"); }
        var http = clients.CreateClient();
        var tokenResponse = await http.PostAsync(discovery.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = payload.RedirectUri,
            ["client_id"] = configuration.ClientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = payload.CodeVerifier,
        }), cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode) return Results.Redirect("/auth/sso?error=token_exchange_failed");
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        if (!tokenJson.RootElement.TryGetProperty("access_token", out var tokenProperty)) return Results.Redirect("/auth/sso?error=token_missing");
        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, discovery.UserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenProperty.GetString());
        var userInfoResponse = await http.SendAsync(userInfoRequest, cancellationToken);
        if (!userInfoResponse.IsSuccessStatusCode) return Results.Redirect("/auth/sso?error=userinfo_failed");
        using var userInfo = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync(cancellationToken));
        var subject = Property(userInfo.RootElement, "sub");
        var email = Property(userInfo.RootElement, "email");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email) ||
            userInfo.RootElement.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.False)
            return Results.Redirect("/auth/sso?error=identity_invalid");
        var domain = email[(email.LastIndexOf('@') + 1)..].ToLowerInvariant();
        if (configuration.AllowedEmailDomains.Length > 0 && !configuration.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            return Results.Redirect("/auth/sso?error=domain_not_allowed");
        tenantContext.TenantId = payload.TenantId;
        var link = await dbContext.ExternalIdentityLinks.SingleOrDefaultAsync(item => item.Provider == "oidc" && item.Subject == subject, cancellationToken);
        User? user;
        if (link is not null)
        {
            user = await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == link.UserId && item.IsActive, cancellationToken);
        }
        else
        {
            var normalized = email.Trim().ToUpperInvariant();
            user = await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.NormalizedEmail == normalized && item.IsActive && item.EmailVerified, cancellationToken);
            if (user is null || !await dbContext.TenantMemberships.AnyAsync(item => item.UserId == user.Id && item.Status == MembershipStatus.Active, cancellationToken))
                return Results.Redirect("/auth/sso?error=membership_required");
            link = new ExternalIdentityLink { TenantId = payload.TenantId, UserId = user.Id, Provider = "oidc", Subject = subject, EmailSnapshot = email };
            dbContext.ExternalIdentityLinks.Add(link);
        }
        if (user is null || !await dbContext.TenantMemberships.AnyAsync(item => item.UserId == user.Id && item.Status == MembershipStatus.Active, cancellationToken))
            return Results.Redirect("/auth/sso?error=membership_required");
        var rawExchange = Base64Url(RandomNumberGenerator.GetBytes(32));
        dbContext.SsoAuthenticationExchanges.Add(new SsoAuthenticationExchange
        {
            TenantId = payload.TenantId,
            UserId = user.Id,
            CodeHash = Hash(rawExchange),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        var separator = payload.ReturnUrl.Contains('?') ? '&' : '?';
        return Results.Redirect($"{payload.ReturnUrl}{separator}ssoCode={Uri.EscapeDataString(rawExchange)}&tenantId={payload.TenantId}");
    }

    private static async Task<IResult> ExchangeAsync(
        SsoExchangeRequest request,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var hash = Hash(request.Code.Trim());
        var exchange = await dbContext.SsoAuthenticationExchanges.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.CodeHash == hash && item.UsedAt == null && item.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        if (exchange is null) return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "The SSO exchange code is invalid or expired.",
            extensions: new Dictionary<string, object?> { ["code"] = "invalid_sso_exchange", ["traceId"] = context.TraceIdentifier });
        tenantContext.TenantId = exchange.TenantId;
        var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == exchange.UserId && item.IsActive, cancellationToken);
        var membership = await dbContext.TenantMemberships.Include(item => item.Tenant).Include(item => item.RoleGrants)
            .SingleAsync(item => item.UserId == user.Id && item.Status == MembershipStatus.Active, cancellationToken);
        var customGrants = await dbContext.CustomRoleGrants.AsNoTracking().Where(item => item.TenantMembershipId == membership.Id && item.CustomRole.IsActive)
            .Include(item => item.CustomRole).Include(item => item.ProjectScopes).ToArrayAsync(cancellationToken);
        var access = new TenantAccessSummary(
            membership.TenantId, membership.Tenant.Name, membership.Tenant.ShortName,
            membership.RoleGrants.Select(item => item.Role).Distinct().ToArray(),
            customGrants.Where(item => item.AllProjects).SelectMany(item => item.CustomRole.Permissions).Distinct().ToArray(),
            customGrants.Select(item => item.CustomRole.Name).Distinct().ToArray(),
            customGrants.Where(item => !item.AllProjects).SelectMany(item => item.ProjectScopes.Select(scope => new { scope.ProjectId, item.CustomRole.Permissions }))
                .GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => (IReadOnlyCollection<string>)group.SelectMany(item => item.Permissions).Distinct().ToArray()));
        var rawSession = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new UserSession
        {
            UserId = user.Id,
            TokenHash = Hash(rawSession),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
            UserAgent = context.Request.Headers.UserAgent.ToString()[..Math.Min(context.Request.Headers.UserAgent.ToString().Length, 512)],
        };
        exchange.UsedAt = DateTimeOffset.UtcNow;
        user.LastSignedInAt = DateTimeOffset.UtcNow;
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new AuthenticatedSession(user.Id, user.Email, user.DisplayName, user.PreferredLanguage,
            user.ThemePreference, rawSession, session.ExpiresAt, [access]));
    }

    private static async Task<OidcDiscovery?> DiscoverAsync(string authority, IHttpClientFactory clients, CancellationToken cancellationToken)
    {
        try
        {
            var response = await clients.CreateClient().GetAsync(authority.TrimEnd('/') + "/.well-known/openid-configuration", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var authorization = Property(json.RootElement, "authorization_endpoint");
            var token = Property(json.RootElement, "token_endpoint");
            var userInfo = Property(json.RootElement, "userinfo_endpoint");
            return Uri.TryCreate(authorization, UriKind.Absolute, out var authorizationUri) && authorizationUri.Scheme == Uri.UriSchemeHttps &&
                   Uri.TryCreate(token, UriKind.Absolute, out var tokenUri) && tokenUri.Scheme == Uri.UriSchemeHttps &&
                   Uri.TryCreate(userInfo, UriKind.Absolute, out var userInfoUri) && userInfoUri.Scheme == Uri.UriSchemeHttps
                ? new OidcDiscovery(authorizationUri, tokenUri, userInfoUri) : null;
        }
        catch (HttpRequestException) { return null; }
    }
    private static string? Property(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record SsoState(Guid TenantId, string CodeVerifier, string RedirectUri, string ReturnUrl, DateTimeOffset ExpiresAt);
    private sealed record OidcDiscovery(Uri AuthorizationEndpoint, Uri TokenEndpoint, Uri UserInfoEndpoint);
}

public sealed record SsoStartResponse(string AuthorizationUrl);
public sealed record SsoExchangeRequest(string Code);
