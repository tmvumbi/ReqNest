using System.Buffers;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReqNest.Core.Storage;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;

namespace ReqNest.Api.Endpoints;

public static class AttachmentEndpoints
{
    private const long MaxFileSize = 25L * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".md"] = "text/markdown",
    };

    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tickets = endpoints.MapGroup("/api/tickets/{ticketId:guid}/attachments")
            .RequireAuthorization()
            .WithTags("Attachments");
        tickets.MapGet("/", ListAsync);
        tickets.MapPost("/", UploadAsync).DisableAntiforgery();

        var attachments = endpoints.MapGroup("/api/attachments")
            .RequireAuthorization()
            .WithTags("Attachments");
        attachments.MapGet("/{attachmentId:guid}", DownloadAsync);
        attachments.MapGet("/{attachmentId:guid}/preview", PreviewAsync);
        attachments.MapDelete("/{attachmentId:guid}", DeleteAsync);
        attachments.MapPost("/{attachmentId:guid}/scan-result", SetScanResultAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (access is not null)
        {
            return access;
        }

        var items = await dbContext.Attachments.AsNoTracking()
            .Where(entity => entity.TicketId == ticketId && entity.DeletedAt == null)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new AttachmentResponse(
                entity.Id,
                entity.TicketCommentId,
                entity.OriginalFileName,
                entity.ContentType,
                entity.Size,
                entity.ChecksumSha256,
                entity.ScanStatus,
                entity.UploadedByUserId,
                entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<AttachmentResponse>>(items);
    }

    private static async Task<IResult> UploadAsync(
        Guid ticketId,
        Guid? commentId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var accessError = await GetAccessAsync(ticketId, httpContext, dbContext, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var ticket = await dbContext.Tickets.AsNoTracking().SingleAsync(entity => entity.Id == ticketId, cancellationToken);
        var authorization = httpContext.TenantAuthorization();
        if (ticket.IsArchived || authorization is null || !authorization.CanMaintainTickets(ticket.ProjectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var storageQuota = await dbContext.Tenants.AsNoTracking()
            .Select(entity => entity.StorageQuotaBytes)
            .SingleAsync(cancellationToken);
        var storageUsed = await dbContext.Attachments.AsNoTracking()
            .Where(entity => entity.DeletedAt == null)
            .SumAsync(entity => (long?)entity.Size, cancellationToken) ?? 0;
        if (httpContext.Request.ContentLength is { } contentLength &&
            contentLength > storageQuota - storageUsed)
        {
            return ApiProblems.Validation(httpContext, "The tenant storage quota would be exceeded.", "storage_quota_exceeded");
        }

        if (commentId is not null &&
            !await dbContext.TicketComments.AnyAsync(entity => entity.Id == commentId && entity.TicketId == ticketId, cancellationToken))
        {
            return ApiProblems.NotFound(httpContext, "Comment");
        }

        var rawFileName = httpContext.Request.Headers["X-File-Name"].ToString();
        var fileName = Path.GetFileName(rawFileName).Trim();
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(fileName) || !AllowedTypes.TryGetValue(extension, out var detectedContentType))
        {
            return ApiProblems.Validation(httpContext, "This file type is not allowed.", "file_type_not_allowed");
        }

        if (httpContext.Request.ContentLength is > MaxFileSize)
        {
            return ApiProblems.Validation(httpContext, "The file exceeds the 25 MB limit.", "file_too_large");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"reqnest-{Guid.NewGuid():N}.upload");
        try
        {
            string checksum;
            long size;
            byte[] signature;
            await using (var output = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                (size, checksum, signature) = await CopyAndInspectAsync(
                    httpContext.Request.Body,
                    output,
                    cancellationToken);
                if (size == 0 || size > MaxFileSize)
                {
                    return ApiProblems.Validation(httpContext, "The file is empty or exceeds the 25 MB limit.", "invalid_file_size");
                }

                if (size > storageQuota - storageUsed)
                {
                    return ApiProblems.Validation(httpContext, "The tenant storage quota would be exceeded.", "storage_quota_exceeded");
                }

                if (!SignatureMatches(extension, signature))
                {
                    return ApiProblems.Validation(httpContext, "The file contents do not match its extension.", "file_signature_mismatch");
                }

                output.Position = 0;
                var options = storageOptions.Value;
                var blobName = $"{ticket.TenantId:N}/{ticket.ProjectId:N}/{ticket.Id:N}/{Guid.NewGuid():N}";
                await blobStorage.UploadAsync(options.DefaultContainer, blobName, output, detectedContentType, cancellationToken);
                var attachment = new Attachment
                {
                    TenantId = ticket.TenantId,
                    ProjectId = ticket.ProjectId,
                    TicketId = ticket.Id,
                    TicketCommentId = commentId,
                    UploadedByUserId = httpContext.User.UserId(),
                    ContainerName = options.DefaultContainer,
                    BlobName = blobName,
                    OriginalFileName = fileName[..Math.Min(fileName.Length, 260)],
                    ContentType = detectedContentType,
                    Size = size,
                    ChecksumSha256 = checksum,
                    ScanStatus = options.MarkDevelopmentUploadsClean
                        ? AttachmentScanStatus.Clean
                        : AttachmentScanStatus.Pending,
                };
                dbContext.Attachments.Add(attachment);
                TicketEndpoints.AddAudit(dbContext, httpContext, ticket, "attachment.uploaded", "An attachment was uploaded.");
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    await blobStorage.DeleteIfExistsAsync(options.DefaultContainer, blobName, cancellationToken);
                    throw;
                }

                return TypedResults.Created($"/api/attachments/{attachment.Id}", new AttachmentResponse(
                    attachment.Id,
                    attachment.TicketCommentId,
                    attachment.OriginalFileName,
                    attachment.ContentType,
                    attachment.Size,
                    attachment.ChecksumSha256,
                    attachment.ScanStatus,
                    attachment.UploadedByUserId,
                    attachment.CreatedAt));
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static async Task<IResult> DownloadAsync(
        Guid attachmentId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == attachmentId && entity.DeletedAt == null, cancellationToken);
        if (attachment is null)
        {
            return ApiProblems.NotFound(httpContext, "Attachment");
        }

        var accessError = await GetAccessAsync(attachment.TicketId, httpContext, dbContext, cancellationToken);
        if (accessError is not null)
        {
            return ApiProblems.NotFound(httpContext, "Attachment");
        }

        if (attachment.ScanStatus != AttachmentScanStatus.Clean)
        {
            return ApiProblems.Conflict(httpContext, "This attachment is not available for download.", "attachment_not_clean");
        }

        var stream = await blobStorage.OpenReadAsync(attachment.ContainerName, attachment.BlobName, cancellationToken);
        return Results.Stream(stream, attachment.ContentType, attachment.OriginalFileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> PreviewAsync(
        Guid attachmentId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == attachmentId && entity.DeletedAt == null, cancellationToken);
        if (attachment is null ||
            !(attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
              attachment.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)))
        {
            return ApiProblems.NotFound(httpContext, "Preview");
        }

        var accessError = await GetAccessAsync(attachment.TicketId, httpContext, dbContext, cancellationToken);
        if (accessError is not null)
        {
            return ApiProblems.NotFound(httpContext, "Preview");
        }

        if (attachment.ScanStatus != AttachmentScanStatus.Clean)
        {
            return ApiProblems.Conflict(httpContext, "This attachment is not available for preview.", "attachment_not_clean");
        }

        var stream = await blobStorage.OpenReadAsync(attachment.ContainerName, attachment.BlobName, cancellationToken);
        httpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self' data:; style-src 'none'; sandbox";
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Stream(stream, attachment.ContentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteAsync(
        Guid attachmentId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(
            entity => entity.Id == attachmentId && entity.DeletedAt == null,
            cancellationToken);
        if (attachment is null)
        {
            return ApiProblems.NotFound(httpContext, "Attachment");
        }

        var authorization = httpContext.TenantAuthorization();
        if (authorization is null || !authorization.CanAccessProject(attachment.ProjectId))
        {
            return ApiProblems.NotFound(httpContext, "Attachment");
        }

        var userId = httpContext.User.UserId();
        if (attachment.UploadedByUserId != userId && !authorization.CanManageProject(attachment.ProjectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        attachment.DeletedAt = DateTimeOffset.UtcNow;
        var ticket = await dbContext.Tickets.SingleAsync(entity => entity.Id == attachment.TicketId, cancellationToken);
        TicketEndpoints.AddAudit(dbContext, httpContext, ticket, "attachment.deleted", "An attachment was deleted.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SetScanResultAsync(
        Guid attachmentId,
        SetAttachmentScanResultRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.IsTenantAdministrator() || request.Status == AttachmentScanStatus.Pending)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(entity => entity.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return ApiProblems.NotFound(httpContext, "Attachment");
        }

        attachment.ScanStatus = request.Status;
        var ticket = await dbContext.Tickets.SingleAsync(entity => entity.Id == attachment.TicketId, cancellationToken);
        TicketEndpoints.AddAudit(dbContext, httpContext, ticket, "attachment.scan.completed", "Attachment scanning completed.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new AttachmentScanResultResponse(attachment.Id, attachment.ScanStatus));
    }

    private static async Task<IResult?> GetAccessAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var projectId = await dbContext.Tickets.AsNoTracking()
            .Where(entity => entity.Id == ticketId)
            .Select(entity => (Guid?)entity.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
        return projectId is null || !authorization.CanAccessProject(projectId.Value)
            ? ApiProblems.NotFound(httpContext, "Ticket")
            : null;
    }

    private static async Task<(long Size, string Checksum, byte[] Signature)> CopyAndInspectAsync(
        Stream input,
        Stream output,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var signature = new byte[16];
        var signatureLength = 0;
        long total = 0;
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxFileSize)
                {
                    break;
                }

                if (signatureLength < signature.Length)
                {
                    var copy = Math.Min(read, signature.Length - signatureLength);
                    buffer.AsSpan(0, copy).CopyTo(signature.AsSpan(signatureLength));
                    signatureLength += copy;
                }

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return (total, Convert.ToHexString(hash.GetHashAndReset()), signature[..signatureLength]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool SignatureMatches(string extension, ReadOnlySpan<byte> signature)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            ".jpg" or ".jpeg" => signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".gif" => signature.StartsWith(new byte[] { 0x47, 0x49, 0x46, 0x38 }),
            ".webp" => signature.Length >= 12 && signature.StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 }) &&
                       signature[8..12].SequenceEqual("WEBP"u8),
            ".pdf" => signature.StartsWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }),
            ".docx" or ".xlsx" or ".pptx" or ".odt" or ".ods" or ".odp" => signature.StartsWith(new byte[] { 0x50, 0x4B }),
            _ => !signature.Contains((byte)0),
        };
    }
}

public sealed record AttachmentResponse(
    Guid Id,
    Guid? CommentId,
    string FileName,
    string ContentType,
    long Size,
    string ChecksumSha256,
    AttachmentScanStatus ScanStatus,
    Guid? UploadedByUserId,
    DateTimeOffset CreatedAt);

public sealed record SetAttachmentScanResultRequest(AttachmentScanStatus Status);

public sealed record AttachmentScanResultResponse(Guid Id, AttachmentScanStatus Status);
