using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReqNest.Core.Identity;
using ReqNest.Core.Auditing;
using ReqNest.Core.Storage;
using ReqNest.Core.Tenancy;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ReqNest.Api.Endpoints;

public static class TenantEndpoints
{
    private const long MaxAvatarSize = 10 * 1024 * 1024;
    private const int MaxAvatarDimension = 512;

    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tenants = endpoints.MapGroup("/api/tenants")
            .RequireAuthorization()
            .WithTags("Tenants");
        tenants.MapGet("/", ListAsync);
        tenants.MapGet("/current", GetCurrentAsync);
        tenants.MapPatch("/current", UpdateCurrentAsync);

        var profile = endpoints.MapGroup("/api/profile")
            .RequireAuthorization()
            .WithTags("Profile");
        profile.MapGet("/", GetProfileAsync);
        profile.MapPatch("/", UpdateProfileAsync);
        profile.MapPatch("/preferences", UpdatePreferencesAsync);
        profile.MapPost("/avatar", UploadAvatarAsync).DisableAntiforgery();
        profile.MapGet("/avatar", DownloadAvatarAsync);
        profile.MapDelete("/avatar", DeleteAvatarAsync);
        return endpoints;
    }

    private static async Task<Ok<ProfileResponse>> GetProfileAsync(
        ClaimsPrincipal principal,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.UserId();
        var user = await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == userId, cancellationToken);
        return TypedResults.Ok(ToProfileResponse(user));
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 120)
        {
            return ApiProblems.Validation(httpContext, "A display name of at most 120 characters is required.");
        }

        var userId = principal.UserId();
        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == userId, cancellationToken);
        user.DisplayName = displayName;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToProfileResponse(user));
    }

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> UploadAvatarAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var contentType = httpContext.Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
        if (contentType is not ("image/png" or "image/jpeg" or "image/webp") ||
            httpContext.Request.ContentLength is > MaxAvatarSize)
        {
            return ApiProblems.Validation(
                httpContext, "Avatars must be PNG, JPEG, or WebP and no larger than 10 MB.", "invalid_avatar");
        }

        await using var buffer = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 or > MaxAvatarSize)
        {
            return ApiProblems.Validation(httpContext, "The avatar contents are invalid.", "invalid_avatar");
        }

        // Any reasonable image is accepted; it is decoded and downscaled to a small
        // square-fitting PNG before storage, so client-side resizing is unnecessary.
        byte[] processed;
        try
        {
            var info = Image.Identify(buffer.ToArray());
            if (info is null || info.Width <= 0 || info.Height <= 0 ||
                (long)info.Width * info.Height > 64_000_000)
            {
                return ApiProblems.Validation(httpContext, "The avatar contents are invalid.", "invalid_avatar");
            }

            using var image = Image.Load(buffer.ToArray());
            var side = Math.Min(MaxAvatarDimension, Math.Min(image.Width, image.Height));
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = new Size(side, side),
            }));

            using var encoded = new MemoryStream();
            await image.SaveAsPngAsync(encoded, cancellationToken);
            processed = encoded.ToArray();
        }
        catch (UnknownImageFormatException)
        {
            return ApiProblems.Validation(httpContext, "The avatar contents are invalid.", "invalid_avatar");
        }
        catch (InvalidImageContentException)
        {
            return ApiProblems.Validation(httpContext, "The avatar contents are invalid.", "invalid_avatar");
        }

        var userId = principal.UserId();
        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == userId, cancellationToken);
        var options = storageOptions.Value;
        var blobName = $"users/{user.Id:N}/avatar/{Guid.NewGuid():N}";
        await using var processedStream = new MemoryStream(processed, writable: false);
        await blobStorage.UploadAsync(
            options.DefaultContainer, blobName, processedStream, "image/png", cancellationToken);
        var oldBlobName = user.AvatarBlobName;
        user.AvatarBlobName = blobName;
        user.AvatarContentType = "image/png";
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await blobStorage.DeleteIfExistsAsync(options.DefaultContainer, blobName, cancellationToken);
            throw;
        }

        if (oldBlobName is not null)
        {
            await blobStorage.DeleteIfExistsAsync(options.DefaultContainer, oldBlobName, cancellationToken);
        }

        return TypedResults.Ok(ToProfileResponse(user));
    }

    private static async Task<IResult> DownloadAvatarAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var userId = principal.UserId();
        var user = await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == userId, cancellationToken);
        if (user.AvatarBlobName is null || user.AvatarContentType is null)
        {
            return ApiProblems.NotFound(httpContext, "Avatar");
        }

        var stream = await blobStorage.OpenReadAsync(
            storageOptions.Value.DefaultContainer, user.AvatarBlobName, cancellationToken);
        return Results.Stream(stream, user.AvatarContentType, enableRangeProcessing: true);
    }

    private static async Task<NoContent> DeleteAvatarAsync(
        ClaimsPrincipal principal,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var userId = principal.UserId();
        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == userId, cancellationToken);
        var blobName = user.AvatarBlobName;
        user.AvatarBlobName = null;
        user.AvatarContentType = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (blobName is not null)
        {
            await blobStorage.DeleteIfExistsAsync(storageOptions.Value.DefaultContainer, blobName, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static ProfileResponse ToProfileResponse(User user) =>
        new(user.DisplayName, user.Email, user.AvatarBlobName is not null);

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

public sealed record ProfileResponse(string DisplayName, string Email, bool HasAvatar);

public sealed record UpdateProfileRequest(string DisplayName);
