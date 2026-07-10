using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ReqNest.Core.Identity;
using ReqNest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ReqNest.Api.Authentication;

public static class SessionAuthenticationDefaults
{
    public const string Scheme = "ReqNestSession";
}

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionValidationService sessionValidationService,
    ReqNestDbContext dbContext)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization[prefix.Length..].Trim();
        var user = await sessionValidationService.ValidateAsync(token, Context.RequestAborted);

        if (user is null)
        {
            return await AuthenticateApiTokenAsync(token);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private async Task<AuthenticateResult> AuthenticateApiTokenAsync(string rawToken)
    {
        if (!rawToken.StartsWith("rqn_", StringComparison.Ordinal) || rawToken.Length != 68)
        {
            return AuthenticateResult.Fail("The access token is invalid or expired.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var now = DateTimeOffset.UtcNow;
        var token = await dbContext.ApiTokens.IgnoreQueryFilters().SingleOrDefaultAsync(
            entity => entity.TokenHash == hash && entity.RevokedAt == null &&
                      (entity.ExpiresAt == null || entity.ExpiresAt > now),
            Context.RequestAborted);
        if (token is null)
        {
            return AuthenticateResult.Fail("The access token is invalid or expired.");
        }

        token.LastUsedAt = now;
        await dbContext.SaveChangesAsync(Context.RequestAborted);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, token.CreatedByUserId.ToString()),
            new("reqnest:api-token", token.Id.ToString()),
            new("reqnest:tenant", token.TenantId.ToString()),
        };
        claims.AddRange(token.Scopes.Select(scope => new Claim("reqnest:scope", scope)));
        claims.AddRange(token.ProjectIds.Select(projectId => new Claim("reqnest:project", projectId.ToString())));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
