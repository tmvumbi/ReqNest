using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Notifications;
using ReqNest.Core.Storage;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/operations")
            .RequireAuthorization()
            .WithTags("Operations");
        group.MapGet("/retention", GetRetentionAsync);
        group.MapPut("/retention", UpdateRetentionAsync);
        group.MapGet("/retention/preview", PreviewRetentionAsync);
        group.MapPost("/retention/run", RunRetentionAsync);
        group.MapGet("/email-outbox", ListOutboxAsync);
        group.MapPost("/email-outbox/{messageId:guid}/retry", RetryOutboxAsync);
        return endpoints;
    }

    private static async Task<IResult> GetRetentionAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(httpContext);
        if (error is not null)
        {
            return error;
        }

        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        var usedBytes = await dbContext.Attachments.AsNoTracking()
            .Where(entity => entity.DeletedAt == null)
            .SumAsync(entity => (long?)entity.Size, cancellationToken) ?? 0;
        return TypedResults.Ok(ToResponse(tenant, usedBytes));
    }

    private static async Task<IResult> UpdateRetentionAsync(
        UpdateRetentionRequest request,
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

        if (request.StorageQuotaBytes is < 26_214_400 or > 10_995_116_277_760 ||
            request.NotificationRetentionDays is < 1 or > 3650 ||
            request.AuditRetentionDays is < 30 or > 3650 ||
            request.DeletedAttachmentRetentionDays is < 1 or > 3650 ||
            request.ReportRetentionDays is < 1 or > 365)
        {
            return ApiProblems.Validation(httpContext, "Quota or retention values are outside supported limits.");
        }

        var usedBytes = await dbContext.Attachments.AsNoTracking()
            .Where(entity => entity.DeletedAt == null)
            .SumAsync(entity => (long?)entity.Size, cancellationToken) ?? 0;
        if (request.StorageQuotaBytes < usedBytes)
        {
            return ApiProblems.Conflict(httpContext, "The quota cannot be lower than current storage usage.", "quota_below_usage");
        }

        var tenant = await dbContext.Tenants.SingleAsync(cancellationToken);
        tenant.StorageQuotaBytes = request.StorageQuotaBytes;
        tenant.NotificationRetentionDays = request.NotificationRetentionDays;
        tenant.AuditRetentionDays = request.AuditRetentionDays;
        tenant.DeletedAttachmentRetentionDays = request.DeletedAttachmentRetentionDays;
        tenant.ReportRetentionDays = request.ReportRetentionDays;
        AddAudit(dbContext, httpContext, authorization.TenantId, "retention.settings.updated", tenant.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(tenant, usedBytes));
    }

    private static async Task<IResult> PreviewRetentionAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(httpContext);
        if (error is not null)
        {
            return error;
        }

        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        return TypedResults.Ok(await BuildPreviewAsync(tenant, dbContext, cancellationToken));
    }

    private static async Task<IResult> RunRetentionAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
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

        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        var preview = await BuildPreviewAsync(tenant, dbContext, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var deletedAttachments = await dbContext.Attachments
            .Where(entity => entity.DeletedAt < now.AddDays(-tenant.DeletedAttachmentRetentionDays))
            .OrderBy(entity => entity.DeletedAt)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        foreach (var attachment in deletedAttachments)
        {
            await blobStorage.DeleteIfExistsAsync(attachment.ContainerName, attachment.BlobName, cancellationToken);
        }

        var expiredReports = await dbContext.ReportExports
            .Where(entity => entity.ExpiresAt < now || entity.CreatedAt < now.AddDays(-tenant.ReportRetentionDays))
            .OrderBy(entity => entity.ExpiresAt)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        foreach (var report in expiredReports.Where(entity => entity.ContainerName != null && entity.BlobName != null))
        {
            await blobStorage.DeleteIfExistsAsync(report.ContainerName!, report.BlobName!, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Attachments.RemoveRange(deletedAttachments);
        dbContext.ReportExports.RemoveRange(expiredReports);
        await dbContext.Notifications
            .Where(entity => entity.CreatedAt < now.AddDays(-tenant.NotificationRetentionDays))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.EmailOutboxMessages
            .Where(entity => entity.Status == EmailOutboxStatus.Sent &&
                             entity.CreatedAt < now.AddDays(-tenant.NotificationRetentionDays))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.AuditEvents
            .Where(entity => entity.CreatedAt < now.AddDays(-tenant.AuditRetentionDays))
            .ExecuteDeleteAsync(cancellationToken);
        AddAudit(dbContext, httpContext, authorization.TenantId, "retention.run.completed", authorization.TenantId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(preview with
        {
            DeletedAttachments = deletedAttachments.Length,
            ReportExports = expiredReports.Length,
        });
    }

    private static async Task<IResult> ListOutboxAsync(
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(httpContext);
        if (error is not null)
        {
            return error;
        }

        var requestedPage = Math.Max(1, page ?? 1);
        var requestedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var total = await dbContext.EmailOutboxMessages.CountAsync(cancellationToken);
        var items = await dbContext.EmailOutboxMessages.AsNoTracking()
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip((requestedPage - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .Select(entity => new EmailOutboxResponse(
                entity.Id,
                entity.RecipientEmail,
                entity.Subject,
                entity.TemplateKey,
                entity.Status,
                entity.Attempts,
                entity.NextAttemptAt,
                entity.SentAt,
                entity.LastError,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new PagedEmailOutboxResponse(items, requestedPage, requestedPageSize, total));
    }

    private static async Task<IResult> RetryOutboxAsync(
        Guid messageId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(httpContext);
        if (error is not null)
        {
            return error;
        }

        var message = await dbContext.EmailOutboxMessages.SingleOrDefaultAsync(
            entity => entity.Id == messageId,
            cancellationToken);
        if (message is null)
        {
            return ApiProblems.NotFound(httpContext, "Email outbox message");
        }

        message.Status = EmailOutboxStatus.Pending;
        message.NextAttemptAt = DateTimeOffset.UtcNow;
        message.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<RetentionPreviewResponse> BuildPreviewAsync(
        Core.Tenancy.Tenant tenant,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var notifications = await dbContext.Notifications.CountAsync(
            entity => entity.CreatedAt < now.AddDays(-tenant.NotificationRetentionDays),
            cancellationToken);
        var auditEvents = await dbContext.AuditEvents.CountAsync(
            entity => entity.CreatedAt < now.AddDays(-tenant.AuditRetentionDays),
            cancellationToken);
        var attachments = await dbContext.Attachments.CountAsync(
            entity => entity.DeletedAt < now.AddDays(-tenant.DeletedAttachmentRetentionDays),
            cancellationToken);
        var reports = await dbContext.ReportExports.CountAsync(
            entity => entity.ExpiresAt < now || entity.CreatedAt < now.AddDays(-tenant.ReportRetentionDays),
            cancellationToken);
        return new RetentionPreviewResponse(notifications, auditEvents, attachments, reports);
    }

    private static IResult? RequireAdministrator(HttpContext context)
    {
        var authorization = context.TenantAuthorization();
        return authorization is null
            ? ApiProblems.TenantRequired(context)
            : authorization.IsTenantAdministrator()
                ? null
                : ApiProblems.Forbidden(context);
    }

    private static RetentionSettingsResponse ToResponse(Core.Tenancy.Tenant tenant, long usedBytes) => new(
        tenant.StorageQuotaBytes,
        usedBytes,
        tenant.NotificationRetentionDays,
        tenant.AuditRetentionDays,
        tenant.DeletedAttachmentRetentionDays,
        tenant.ReportRetentionDays);

    private static void AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Guid tenantId,
        string action,
        Guid targetId) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = "Operations",
            TargetId = targetId.ToString(),
            Summary = "Operational retention configuration or processing changed.",
            CorrelationId = context.TraceIdentifier,
        });
}

public sealed record UpdateRetentionRequest(
    long StorageQuotaBytes,
    int NotificationRetentionDays,
    int AuditRetentionDays,
    int DeletedAttachmentRetentionDays,
    int ReportRetentionDays);

public sealed record RetentionSettingsResponse(
    long StorageQuotaBytes,
    long StorageUsedBytes,
    int NotificationRetentionDays,
    int AuditRetentionDays,
    int DeletedAttachmentRetentionDays,
    int ReportRetentionDays);

public sealed record RetentionPreviewResponse(
    int Notifications,
    int AuditEvents,
    int DeletedAttachments,
    int ReportExports);

public sealed record EmailOutboxResponse(
    Guid Id,
    string RecipientEmail,
    string Subject,
    string TemplateKey,
    EmailOutboxStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? SentAt,
    string? LastError,
    DateTimeOffset CreatedAt);

public sealed record PagedEmailOutboxResponse(
    IReadOnlyCollection<EmailOutboxResponse> Items,
    int Page,
    int PageSize,
    int Total);
