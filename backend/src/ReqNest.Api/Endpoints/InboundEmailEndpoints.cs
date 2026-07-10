using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReqNest.Core.Auditing;
using ReqNest.Core.Configuration;
using ReqNest.Core.Content;
using ReqNest.Core.Integrations;
using ReqNest.Core.Notifications;
using ReqNest.Core.Storage;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;

namespace ReqNest.Api.Endpoints;

public static class InboundEmailEndpoints
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
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
    };

    public static IEndpointRouteBuilder MapInboundEmailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/public/inbound-email/{channelId:guid}", ReceiveAsync)
            .AllowAnonymous().RequireRateLimiting("authentication").WithTags("Inbound email");
        return endpoints;
    }

    private static async Task<IResult> ReceiveAsync(
        Guid channelId,
        InboundEmailRequest request,
        HttpContext context,
        ITenantContext tenantContext,
        ReqNestDbContext dbContext,
        IRichContentSanitizer sanitizer,
        ISlaCalculator slaCalculator,
        INotificationService notificationService,
        IWebhookEventPublisher webhookPublisher,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        var channel = await dbContext.InboundEmailChannels.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == channelId && item.IsActive, cancellationToken);
        var suppliedSecret = context.Request.Headers["X-Inbound-Secret"].ToString();
        if (channel is null || suppliedSecret.Length < 32 || !FixedEquals(channel.SecretHash, Hash(suppliedSecret)))
        {
            return Results.NotFound();
        }

        tenantContext.TenantId = channel.TenantId;
        var messageId = request.MessageId.Trim();
        if (messageId.Length is < 3 or > 500 || !System.Net.Mail.MailAddress.TryCreate(request.SenderEmail, out _) ||
            string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 300)
        {
            return ApiProblems.Validation(context, "A message ID, sender, and subject are required.");
        }

        if (request.AutoSubmitted || request.SenderEmail.Equals(channel.Address, StringComparison.OrdinalIgnoreCase))
        {
            await RecordRejectedAsync(dbContext, channel, request, "automated_or_loop", cancellationToken);
            return TypedResults.Accepted((string?)null, new { status = "rejected", code = "automated_or_loop" });
        }

        var duplicate = await dbContext.InboundEmailMessages.AnyAsync(item => item.ChannelId == channelId && item.MessageId == messageId, cancellationToken);
        if (duplicate) return TypedResults.Ok(new { status = "duplicate" });

        var content = sanitizer.Sanitize(request.Body);
        if (string.IsNullOrWhiteSpace(content.PlainText)) return ApiProblems.Validation(context, "The email body is empty.");
        var normalizedEmail = request.SenderEmail.Trim().ToUpperInvariant();
        var requester = await dbContext.RequesterIdentities.SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        requester ??= new RequesterIdentity
        {
            TenantId = channel.TenantId,
            Email = request.SenderEmail.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(request.SenderName) ? request.SenderEmail.Trim() : request.SenderName.Trim()[..Math.Min(request.SenderName.Trim().Length, 160)],
            PreferredLanguage = request.Language,
        };
        if (dbContext.Entry(requester).State == EntityState.Detached) dbContext.RequesterIdentities.Add(requester);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Ticket ticket;
        var parent = string.IsNullOrWhiteSpace(request.InReplyTo) ? null : await dbContext.InboundEmailMessages
            .SingleOrDefaultAsync(item => item.ChannelId == channelId && item.MessageId == request.InReplyTo && item.TicketId != null, cancellationToken);
        string eventType;
        AuditEvent audit;
        if (parent?.TicketId is { } parentTicketId)
        {
            ticket = await dbContext.Tickets.SingleAsync(item => item.Id == parentTicketId, cancellationToken);
            var comment = new RequesterComment
            {
                TenantId = channel.TenantId,
                TicketId = ticket.Id,
                RequesterIdentityId = requester.Id,
                Body = content.Html,
                BodyPlainText = content.PlainText,
            };
            dbContext.RequesterComments.Add(comment);
            eventType = "ticket.commented";
            audit = Audit(channel.TenantId, ticket.Id, "inbound_email.reply_added", "An inbound email reply was added to the ticket.", context);
        }
        else
        {
            var project = await dbContext.Projects.FromSqlInterpolated($"SELECT * FROM projects WHERE id = {channel.ProjectId} FOR UPDATE")
                .SingleAsync(cancellationToken);
            var initialStatus = await dbContext.WorkflowStatuses.SingleAsync(item => item.WorkflowId == project.WorkflowId && item.IsInitial, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var sla = await slaCalculator.CalculateAsync(project.Id, channel.DefaultPriorityKey, now, cancellationToken);
            ticket = new Ticket
            {
                TenantId = channel.TenantId,
                ProjectId = project.Id,
                Number = project.NextTicketNumber,
                Key = $"{project.Key}-{project.NextTicketNumber}",
                Title = request.Subject.Trim(),
                Description = content.Html,
                DescriptionPlainText = content.PlainText,
                TypeKey = channel.DefaultTypeKey,
                PriorityKey = channel.DefaultPriorityKey,
                Type = Enum.TryParse<TicketType>(channel.DefaultTypeKey, out var type) ? type : TicketType.ServiceRequest,
                Priority = Enum.TryParse<TicketPriority>(channel.DefaultPriorityKey, out var priority) ? priority : TicketPriority.Normal,
                WorkflowStatusId = initialStatus.Id,
                RequesterIdentityId = requester.Id,
                ReporterEmailSnapshot = requester.Email,
                ReporterDisplayNameSnapshot = requester.DisplayName,
                AssigneeUserId = project.DefaultAssigneeUserId,
                FirstResponseTargetAt = sla?.FirstResponseTargetAt,
                ResolutionTargetAt = sla?.ResolutionTargetAt,
                SlaPolicyId = sla?.PolicyId,
                SlaPolicyNameSnapshot = sla?.PolicyName,
                SlaWarningAt = sla?.WarningAt,
                SlaState = sla is null ? SlaState.None : SlaState.OnTrack,
            };
            project.NextTicketNumber++;
            ticket.StatusHistory.Add(new TicketStatusHistory { TenantId = channel.TenantId, Ticket = ticket, TicketId = ticket.Id, ToStatusId = initialStatus.Id });
            dbContext.Tickets.Add(ticket);
            eventType = "ticket.created";
            audit = Audit(channel.TenantId, ticket.Id, "inbound_email.ticket_created", "An inbound email created a ticket.", context);
        }

        dbContext.AuditEvents.Add(audit);
        var inbound = new InboundEmailMessage
        {
            TenantId = channel.TenantId,
            ChannelId = channel.Id,
            TicketId = ticket.Id,
            MessageId = messageId,
            InReplyTo = request.InReplyTo?.Trim(),
            SenderEmail = requester.Email,
            Subject = request.Subject.Trim(),
            Status = InboundEmailStatus.Processed,
        };
        dbContext.InboundEmailMessages.Add(inbound);
        await StoreAttachmentsAsync(request.Attachments ?? [], requester.Id, ticket, dbContext, blobStorage, storageOptions.Value, cancellationToken);
        await webhookPublisher.PublishAsync(channel.TenantId, eventType, audit.Id.ToString(), new
        {
            ticket.Id,
            ticket.Key,
            ticket.ProjectId,
            Source = "inbound-email",
            inbound.MessageId,
        }, cancellationToken);
        var recipients = await InternalRecipientsAsync(dbContext, ticket, cancellationToken);
        await notificationService.AddAsync(new NotificationMessage(
            channel.TenantId, recipients, null,
            eventType == "ticket.created" ? NotificationType.TicketAssigned : NotificationType.TicketCommented,
            ticket.ProjectId, ticket.Id, audit.Id.ToString(),
            eventType == "ticket.created" ? $"{ticket.Key} was created from email." : $"A requester replied to {ticket.Key} by email.",
            eventType == "ticket.created" ? $"{ticket.Key} a été créé depuis un courriel." : $"Un demandeur a répondu à {ticket.Key} par courriel.",
            $"/app/tickets/{ticket.Id}", ticket.Id.ToString()), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Accepted((string?)null, new { status = "processed", ticket.Id, ticket.Key, threaded = parent is not null });
    }

    private static async Task StoreAttachmentsAsync(
        IReadOnlyCollection<InboundEmailAttachmentRequest> files, Guid requesterId, Ticket ticket,
        ReqNestDbContext dbContext, IBlobStorageService storage, BlobStorageOptions options, CancellationToken cancellationToken)
    {
        var storageQuota = await dbContext.Tenants.AsNoTracking()
            .Select(entity => entity.StorageQuotaBytes)
            .SingleAsync(cancellationToken);
        var storageUsed = await dbContext.Attachments.AsNoTracking()
            .Where(entity => entity.DeletedAt == null)
            .SumAsync(entity => (long?)entity.Size, cancellationToken) ?? 0;

        foreach (var file in files.Take(20))
        {
            var name = Path.GetFileName(file.FileName).Trim();
            var extension = Path.GetExtension(name);
            if (!AllowedTypes.TryGetValue(extension, out var contentType)) throw new BadHttpRequestException("Inbound attachment type is not allowed.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(file.ContentBase64); }
            catch (FormatException) { throw new BadHttpRequestException("Inbound attachment encoding is invalid."); }
            if (bytes.Length == 0 || bytes.LongLength > MaxFileSize || !SignatureMatches(extension, bytes.AsSpan(0, Math.Min(16, bytes.Length))))
                throw new BadHttpRequestException("Inbound attachment content is invalid.");
            if (bytes.LongLength > storageQuota - storageUsed)
                throw new BadHttpRequestException("The tenant storage quota would be exceeded.");

            var blobName = $"{ticket.TenantId:N}/{ticket.ProjectId:N}/{ticket.Id:N}/{Guid.NewGuid():N}";
            await using var stream = new MemoryStream(bytes, writable: false);
            await storage.UploadAsync(options.DefaultContainer, blobName, stream, contentType, cancellationToken);
            dbContext.Attachments.Add(new Attachment
            {
                TenantId = ticket.TenantId,
                ProjectId = ticket.ProjectId,
                TicketId = ticket.Id,
                RequesterIdentityId = requesterId,
                ContainerName = options.DefaultContainer,
                BlobName = blobName,
                OriginalFileName = name[..Math.Min(name.Length, 260)],
                ContentType = contentType,
                Size = bytes.Length,
                ChecksumSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                ScanStatus = options.MarkDevelopmentUploadsClean ? AttachmentScanStatus.Clean : AttachmentScanStatus.Pending,
            });
            storageUsed += bytes.LongLength;
        }
    }

    private static async Task<Guid[]> InternalRecipientsAsync(ReqNestDbContext dbContext, Ticket ticket, CancellationToken cancellationToken) =>
        await dbContext.TicketWatchers.Where(item => item.TicketId == ticket.Id && !item.IsMuted).Select(item => item.UserId)
            .Concat(dbContext.Tickets.Where(item => item.Id == ticket.Id && item.AssigneeUserId != null).Select(item => item.AssigneeUserId!.Value))
            .Distinct().ToArrayAsync(cancellationToken);

    private static async Task RecordRejectedAsync(ReqNestDbContext dbContext, InboundEmailChannel channel, InboundEmailRequest request, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId)) return;
        dbContext.InboundEmailMessages.Add(new InboundEmailMessage
        {
            TenantId = channel.TenantId,
            ChannelId = channel.Id,
            MessageId = request.MessageId.Trim()[..Math.Min(request.MessageId.Trim().Length, 500)],
            SenderEmail = request.SenderEmail.Trim()[..Math.Min(request.SenderEmail.Trim().Length, 320)],
            Subject = request.Subject.Trim()[..Math.Min(request.Subject.Trim().Length, 300)],
            Status = InboundEmailStatus.Rejected,
            FailureCode = code,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditEvent Audit(Guid tenantId, Guid ticketId, string action, string summary, HttpContext context) => new()
    {
        TenantId = tenantId,
        Action = action,
        TargetType = nameof(Ticket),
        TargetId = ticketId.ToString(),
        Summary = summary,
        CorrelationId = context.TraceIdentifier,
    };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool FixedEquals(string left, string right) => left.Length == right.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static bool SignatureMatches(string extension, ReadOnlySpan<byte> signature) => extension.ToLowerInvariant() switch
    {
        ".png" => signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        ".jpg" or ".jpeg" => signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        ".gif" => signature.StartsWith("GIF8"u8),
        ".webp" => signature.Length >= 12 && signature.StartsWith("RIFF"u8) && signature[8..12].SequenceEqual("WEBP"u8),
        ".pdf" => signature.StartsWith("%PDF"u8),
        ".docx" or ".xlsx" or ".pptx" => signature.StartsWith("PK"u8),
        _ => !signature.Contains((byte)0),
    };
}

public sealed record InboundEmailRequest(
    string MessageId, string? InReplyTo, string SenderEmail, string? SenderName, string Subject, string Body,
    ReqNest.Core.Identity.AppLanguage Language, bool AutoSubmitted, IReadOnlyCollection<InboundEmailAttachmentRequest>? Attachments);
public sealed record InboundEmailAttachmentRequest(string FileName, string ContentBase64);
