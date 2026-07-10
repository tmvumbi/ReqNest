using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using ReqNest.Core.Identity;

namespace ReqNest.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("authentication");

        group.MapPost("/register-tenant", RegisterTenantAsync)
            .AllowAnonymous()
            .WithName("RegisterTenant")
            .WithSummary("Creates a tenant and its first administrator.");
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Creates an opaque, revocable user session.");
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout");
        group.MapPost("/logout-all", LogoutAllAsync)
            .RequireAuthorization()
            .WithName("LogoutAll");
        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .WithName("GetCurrentUser");
        group.MapPost("/request-password-reset", RequestPasswordResetAsync)
            .AllowAnonymous()
            .WithName("RequestPasswordReset");
        group.MapPost("/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .WithName("ResetPassword");
        group.MapPost("/verify-email", VerifyEmailAsync)
            .AllowAnonymous()
            .WithName("VerifyEmail");
        group.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword");

        return endpoints;
    }

    private static async Task<Results<Ok<AuthenticatedSession>, ProblemHttpResult>> RegisterTenantAsync(
        RegisterTenantRequest request,
        HttpContext httpContext,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterTenantAsync(
            new RegisterTenantCommand(
                request.CompanyName,
                request.CompanyShortName,
                request.DisplayName,
                request.Email,
                request.Password,
                request.Language,
                request.TimeZone),
            httpContext.Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return result.Session is not null
            ? TypedResults.Ok(result.Session)
            : ToProblem(result.ErrorCode);
    }

    private static async Task<Results<Ok<AuthenticatedSession>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            new LoginCommand(
                request.Email,
                request.Password,
                httpContext.Request.Headers.UserAgent.ToString()),
            cancellationToken);

        return result.Session is not null
            ? TypedResults.Ok(result.Session)
            : ToProblem(result.ErrorCode);
    }

    private static async Task<NoContent> LogoutAsync(
        HttpContext httpContext,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var accessToken = ReadBearerToken(httpContext.Request.Headers.Authorization.ToString());
        if (accessToken is not null)
        {
            await authenticationService.LogoutAsync(accessToken, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LogoutAllAsync(
        ClaimsPrincipal principal,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await authenticationService.LogoutAllAsync(userId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static Ok<CurrentUserResponse> GetCurrentUser(ClaimsPrincipal principal) =>
        TypedResults.Ok(new CurrentUserResponse(
            Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
            principal.FindFirstValue(ClaimTypes.Email)!,
            principal.FindFirstValue(ClaimTypes.Name)!));

    private static async Task<Accepted<PasswordResetRequestedResponse>> RequestPasswordResetAsync(
        PasswordResetRequest request,
        IAuthenticationService authenticationService,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RequestPasswordResetAsync(request.Email, cancellationToken);
        return TypedResults.Accepted(
            (string?)null,
            new PasswordResetRequestedResponse(
                "If the account exists, password reset instructions have been prepared.",
                environment.IsDevelopment() ? result.RawToken : null));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetPasswordAsync(
        ResetPasswordRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        return result.Succeeded ? TypedResults.NoContent() : AccountActionProblem(result.ErrorCode);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> VerifyEmailAsync(
        VerifyEmailRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyEmailAsync(request.Token, cancellationToken);
        return result.Succeeded ? TypedResults.NoContent() : AccountActionProblem(result.ErrorCode);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.ChangePasswordAsync(
            principal.UserId(),
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
        return result.Succeeded ? TypedResults.NoContent() : AccountActionProblem(result.ErrorCode);
    }

    private static ProblemHttpResult AccountActionProblem(string? errorCode) => TypedResults.Problem(
        statusCode: errorCode == "invalid_credentials" ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest,
        title: errorCode switch
        {
            "invalid_password" => "The new password does not meet the security requirements.",
            "invalid_credentials" => "The current password is invalid.",
            _ => "The token is invalid or has expired.",
        },
        extensions: new Dictionary<string, object?> { ["code"] = errorCode });

    private static ProblemHttpResult ToProblem(string? errorCode) => errorCode switch
    {
        "invalid_registration" => TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Registration data is invalid.",
            extensions: new Dictionary<string, object?> { ["code"] = errorCode }),
        "email_already_registered" => TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "This email address is already registered.",
            extensions: new Dictionary<string, object?> { ["code"] = errorCode }),
        "no_active_membership" => TypedResults.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "This account has no active company membership.",
            extensions: new Dictionary<string, object?> { ["code"] = errorCode }),
        _ => TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "The email or password is invalid.",
            extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" }),
    };

    private static string? ReadBearerToken(string authorization)
    {
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}

public sealed record RegisterTenantRequest(
    string CompanyName,
    string CompanyShortName,
    string DisplayName,
    string Email,
    string Password,
    AppLanguage Language,
    string TimeZone);

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserResponse(Guid UserId, string Email, string DisplayName);

public sealed record PasswordResetRequest(string Email);

public sealed record PasswordResetRequestedResponse(string Message, string? DevelopmentToken);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record VerifyEmailRequest(string Token);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
