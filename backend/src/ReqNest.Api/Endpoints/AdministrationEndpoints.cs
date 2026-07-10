using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReqNest.Core.Auditing;
using ReqNest.Core.Storage;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;
using SixLabors.ImageSharp;

namespace ReqNest.Api.Endpoints;

public static class AdministrationEndpoints
{
    private const long MaxLogoSize = 2L * 1024 * 1024;
    private const int MaxLogoDimension = 2_000;

    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var audit = endpoints.MapGroup("/api/audit")
            .RequireAuthorization()
            .WithTags("Administration");
        audit.MapGet("/", ListAuditAsync);
        audit.MapGet("/export", ExportAuditAsync);
        audit.MapGet("/export.csv", ExportAuditCsvAsync);

        var branding = endpoints.MapGroup("/api/tenants/current/logos")
            .RequireAuthorization()
            .WithTags("Branding");
        branding.MapPost("/{variant}", UploadLogoAsync).DisableAntiforgery();
        branding.MapGet("/{variant}", DownloadLogoAsync);
        branding.MapDelete("/{variant}", DeleteLogoAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAuditAsync(
        string? action,
        string? targetType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        var query = FilterAudit(dbContext.AuditEvents.AsNoTracking(), action, targetType, actorUserId, from, to);
        var requestedPage = Math.Max(1, page ?? 1);
        var requestedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(entity => entity.CreatedAt)
            .Skip((requestedPage - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .Select(entity => new AuditEventResponse(
                entity.Id,
                entity.ActorUserId,
                entity.Action,
                entity.TargetType,
                entity.TargetId,
                entity.Summary,
                entity.CorrelationId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new PagedAuditResponse(items, requestedPage, requestedPageSize, total));
    }

    private static async Task<IResult> ExportAuditAsync(
        string? action,
        string? targetType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        var items = await FilterAudit(dbContext.AuditEvents.AsNoTracking(), action, targetType, actorUserId, from, to)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(20_000)
            .Select(entity => new AuditEventResponse(
                entity.Id,
                entity.ActorUserId,
                entity.Action,
                entity.TargetType,
                entity.TargetId,
                entity.Summary,
                entity.CorrelationId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<AuditEventResponse>>(items);
    }

    private static async Task<IResult> ExportAuditCsvAsync(
        string? action,
        string? targetType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true &&
            authorization?.AllProjectPermissions.Contains(
                ReqNest.Core.Identity.AppPermission.AuditView,
                StringComparer.Ordinal) != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        var items = await FilterAudit(dbContext.AuditEvents.AsNoTracking(), action, targetType, actorUserId, from, to)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(20_000)
            .Select(entity => new AuditEventResponse(
                entity.Id,
                entity.ActorUserId,
                entity.Action,
                entity.TargetType,
                entity.TargetId,
                entity.Summary,
                entity.CorrelationId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        var csv = new StringBuilder("Id,ActorUserId,Action,TargetType,TargetId,Summary,CorrelationId,CreatedAt\r\n");
        foreach (var item in items)
        {
            csv.AppendJoin(',',
                Csv(item.Id.ToString()),
                Csv(item.ActorUserId?.ToString() ?? string.Empty),
                Csv(item.Action),
                Csv(item.TargetType),
                Csv(item.TargetId),
                Csv(item.Summary),
                Csv(item.CorrelationId ?? string.Empty),
                Csv(item.CreatedAt.ToString("O")));
            csv.Append("\r\n");
        }

        return Results.File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv; charset=utf-8",
            $"reqnest-audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static async Task<IResult> UploadLogoAsync(
        string variant,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        variant = variant.ToLowerInvariant();
        if (variant is not ("light" or "dark"))
        {
            return ApiProblems.NotFound(httpContext, "Logo variant");
        }

        var contentType = httpContext.Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
        if (contentType is not ("image/png" or "image/jpeg" or "image/webp") ||
            httpContext.Request.ContentLength is > MaxLogoSize)
        {
            return ApiProblems.Validation(httpContext, "Logos must be PNG, JPEG, or WebP and no larger than 2 MB.", "invalid_logo");
        }

        await using var buffer = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 or > MaxLogoSize || !LogoSignatureMatches(contentType, buffer.GetBuffer().AsSpan(0, (int)Math.Min(16, buffer.Length))))
        {
            return ApiProblems.Validation(httpContext, "The logo contents are invalid.", "invalid_logo");
        }

        try
        {
            var image = Image.Identify(buffer.ToArray());
            if (image is null || image.Width > MaxLogoDimension || image.Height > MaxLogoDimension)
            {
                return ApiProblems.Validation(httpContext, "Logos must be no larger than 2000 by 2000 pixels.", "invalid_logo_dimensions");
            }
        }
        catch (UnknownImageFormatException)
        {
            return ApiProblems.Validation(httpContext, "The logo contents are invalid.", "invalid_logo");
        }
        catch (InvalidImageContentException)
        {
            return ApiProblems.Validation(httpContext, "The logo contents are invalid.", "invalid_logo");
        }

        var tenant = await dbContext.Tenants.SingleAsync(cancellationToken);
        var options = storageOptions.Value;
        var blobName = $"{tenant.Id:N}/branding/{variant}/{Guid.NewGuid():N}";
        buffer.Position = 0;
        await blobStorage.UploadAsync(options.DefaultContainer, blobName, buffer, contentType, cancellationToken);
        var oldBlobName = variant == "light" ? tenant.LogoBlobName : tenant.DarkLogoBlobName;
        if (variant == "light")
        {
            tenant.LogoBlobName = blobName;
            tenant.LogoContentType = contentType;
        }
        else
        {
            tenant.DarkLogoBlobName = blobName;
            tenant.DarkLogoContentType = contentType;
        }

        dbContext.AuditEvents.Add(NewTenantAudit(httpContext, tenant.Id, "tenant.logo.updated", "A company logo was updated."));
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

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DownloadLogoAsync(
        string variant,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        var blobName = variant.ToLowerInvariant() == "dark" ? tenant.DarkLogoBlobName : tenant.LogoBlobName;
        var contentType = variant.ToLowerInvariant() == "dark" ? tenant.DarkLogoContentType : tenant.LogoContentType;
        if (blobName is null || contentType is null)
        {
            return ApiProblems.NotFound(httpContext, "Logo");
        }

        var stream = await blobStorage.OpenReadAsync(storageOptions.Value.DefaultContainer, blobName, cancellationToken);
        return Results.Stream(stream, contentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteLogoAsync(
        string variant,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(httpContext) : ApiProblems.Forbidden(httpContext);
        }

        variant = variant.ToLowerInvariant();
        if (variant is not ("light" or "dark"))
        {
            return ApiProblems.NotFound(httpContext, "Logo variant");
        }

        var tenant = await dbContext.Tenants.SingleAsync(cancellationToken);
        var blobName = variant == "dark" ? tenant.DarkLogoBlobName : tenant.LogoBlobName;
        if (variant == "dark")
        {
            tenant.DarkLogoBlobName = null;
            tenant.DarkLogoContentType = null;
        }
        else
        {
            tenant.LogoBlobName = null;
            tenant.LogoContentType = null;
        }

        dbContext.AuditEvents.Add(NewTenantAudit(httpContext, tenant.Id, "tenant.logo.deleted", "A company logo was deleted."));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (blobName is not null)
        {
            await blobStorage.DeleteIfExistsAsync(storageOptions.Value.DefaultContainer, blobName, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static IQueryable<AuditEvent> FilterAudit(
        IQueryable<AuditEvent> query,
        string? action,
        string? targetType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(entity => entity.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(entity => entity.TargetType == targetType);
        }

        if (actorUserId is not null)
        {
            query = query.Where(entity => entity.ActorUserId == actorUserId);
        }

        if (from is not null)
        {
            query = query.Where(entity => entity.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(entity => entity.CreatedAt < to);
        }

        return query;
    }

    private static bool LogoSignatureMatches(string contentType, ReadOnlySpan<byte> signature) => contentType switch
    {
        "image/png" => signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        "image/jpeg" => signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        "image/webp" => signature.Length >= 12 && signature.StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 }) &&
                        signature[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

    private static AuditEvent NewTenantAudit(HttpContext context, Guid tenantId, string action, string summary) => new()
    {
        TenantId = tenantId,
        ActorUserId = context.User.UserId(),
        Action = action,
        TargetType = "Tenant",
        TargetId = tenantId.ToString(),
        Summary = summary,
        CorrelationId = context.TraceIdentifier,
    };
}

public sealed record AuditEventResponse(
    Guid Id,
    Guid? ActorUserId,
    string Action,
    string TargetType,
    string TargetId,
    string Summary,
    string? CorrelationId,
    DateTimeOffset CreatedAt);

public sealed record PagedAuditResponse(
    IReadOnlyCollection<AuditEventResponse> Items,
    int Page,
    int PageSize,
    int Total);
