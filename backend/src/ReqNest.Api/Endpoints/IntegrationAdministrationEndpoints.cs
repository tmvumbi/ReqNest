using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class IntegrationAdministrationEndpoints
{
    private static readonly string[] WebhookEvents =
    [
        "ticket.created", "ticket.updated", "ticket.transitioned", "ticket.commented", "attachment.created",
    ];

    public static IEndpointRouteBuilder MapIntegrationAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/integrations")
            .RequireAuthorization()
            .WithTags("Integrations");
        group.MapGet("/inbound-email", ListEmailChannelsAsync);
        group.MapPost("/inbound-email", CreateEmailChannelAsync);
        group.MapPut("/inbound-email/{channelId:guid}", UpdateEmailChannelAsync);
        group.MapGet("/webhooks", ListWebhooksAsync);
        group.MapPost("/webhooks", CreateWebhookAsync);
        group.MapPut("/webhooks/{subscriptionId:guid}", UpdateWebhookAsync);
        group.MapPost("/webhooks/{subscriptionId:guid}/test", TestWebhookAsync);
        group.MapGet("/webhooks/deliveries", ListWebhookDeliveriesAsync);
        group.MapGet("/connections", ListConnectionsAsync);
        group.MapPost("/connections", UpsertConnectionAsync);
        group.MapPost("/connections/{connectionId:guid}/test", TestConnectionAsync);
        group.MapGet("/sso", GetSsoAsync);
        group.MapPut("/sso", UpdateSsoAsync);
        group.MapPost("/sso/test", TestSsoAsync);
        group.MapGet("/ai", GetAiAsync);
        group.MapPut("/ai", UpdateAiAsync);
        return endpoints;
    }

    private static IResult? RequireAdministrator(HttpContext context)
    {
        var authorization = context.TenantAuthorization();
        return authorization is null
            ? ApiProblems.TenantRequired(context)
            : authorization.IsTenantAdministrator() && !context.User.HasClaim(claim => claim.Type == "reqnest:api-token")
                ? null
                : ApiProblems.Forbidden(context);
    }

    private static async Task<IResult> ListEmailChannelsAsync(
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var channels = await dbContext.InboundEmailChannels.AsNoTracking().OrderBy(entity => entity.Address)
            .Select(entity => new EmailChannelResponse(
                entity.Id, entity.ProjectId, entity.Address, entity.DefaultTypeKey,
                entity.DefaultPriorityKey, entity.IsActive, entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<EmailChannelResponse>>(channels);
    }

    private static async Task<IResult> CreateEmailChannelAsync(
        UpsertEmailChannelRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        if (!System.Net.Mail.MailAddress.TryCreate(request.Address, out _) ||
            !await dbContext.Projects.AnyAsync(entity => entity.Id == request.ProjectId && !entity.IsArchived, cancellationToken))
        {
            return ApiProblems.Validation(context, "A valid channel address and active project are required.");
        }

        var rawSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var authorization = context.TenantAuthorization()!;
        var channel = new InboundEmailChannel
        {
            TenantId = authorization.TenantId,
            ProjectId = request.ProjectId,
            Address = request.Address.Trim().ToLowerInvariant(),
            SecretHash = Hash(rawSecret),
            DefaultTypeKey = request.DefaultTypeKey.Trim(),
            DefaultPriorityKey = request.DefaultPriorityKey.Trim(),
            IsActive = request.IsActive,
        };
        dbContext.InboundEmailChannels.Add(channel);
        AddAudit(dbContext, context, authorization.TenantId, "inbound_email.created", channel.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/integrations/inbound-email/{channel.Id}",
            new EmailChannelCreatedResponse(
                new EmailChannelResponse(channel.Id, channel.ProjectId, channel.Address, channel.DefaultTypeKey,
                    channel.DefaultPriorityKey, channel.IsActive, channel.CreatedAt),
                rawSecret));
    }

    private static async Task<IResult> UpdateEmailChannelAsync(
        Guid channelId,
        UpsertEmailChannelRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var channel = await dbContext.InboundEmailChannels.SingleOrDefaultAsync(entity => entity.Id == channelId, cancellationToken);
        if (channel is null) return ApiProblems.NotFound(context, "Inbound email channel");
        channel.ProjectId = request.ProjectId;
        channel.Address = request.Address.Trim().ToLowerInvariant();
        channel.DefaultTypeKey = request.DefaultTypeKey.Trim();
        channel.DefaultPriorityKey = request.DefaultPriorityKey.Trim();
        channel.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListWebhooksAsync(HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var items = await dbContext.WebhookSubscriptions.AsNoTracking().OrderBy(entity => entity.Name)
            .Select(entity => new WebhookResponse(entity.Id, entity.Name, entity.Url, entity.EventTypes, entity.IsActive, entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<WebhookResponse>>(items);
    }

    private static async Task<IResult> CreateWebhookAsync(
        UpsertWebhookRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        IDataProtectionProvider protectionProvider,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        if (!ValidWebhook(request, out var uri)) return ApiProblems.Validation(context, "A valid HTTPS URL and supported events are required.");
        var rawSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var authorization = context.TenantAuthorization()!;
        var item = new WebhookSubscription
        {
            TenantId = authorization.TenantId,
            Name = request.Name.Trim(),
            Url = uri!.ToString(),
            ProtectedSecret = protectionProvider.CreateProtector("ReqNest.Webhooks.v1").Protect(rawSecret),
            EventTypes = request.EventTypes.Distinct(StringComparer.Ordinal).ToArray(),
            IsActive = request.IsActive,
        };
        dbContext.WebhookSubscriptions.Add(item);
        AddAudit(dbContext, context, authorization.TenantId, "webhook.created", item.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/integrations/webhooks/{item.Id}", new WebhookCreatedResponse(
            new WebhookResponse(item.Id, item.Name, item.Url, item.EventTypes, item.IsActive, item.CreatedAt), rawSecret));
    }

    private static async Task<IResult> UpdateWebhookAsync(
        Guid subscriptionId,
        UpsertWebhookRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        if (!ValidWebhook(request, out var uri)) return ApiProblems.Validation(context, "A valid HTTPS URL and supported events are required.");
        var item = await dbContext.WebhookSubscriptions.SingleOrDefaultAsync(entity => entity.Id == subscriptionId, cancellationToken);
        if (item is null) return ApiProblems.NotFound(context, "Webhook");
        item.Name = request.Name.Trim();
        item.Url = uri!.ToString();
        item.EventTypes = request.EventTypes.Distinct(StringComparer.Ordinal).ToArray();
        item.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> TestWebhookAsync(
        Guid subscriptionId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var item = await dbContext.WebhookSubscriptions.SingleOrDefaultAsync(entity => entity.Id == subscriptionId, cancellationToken);
        if (item is null) return ApiProblems.NotFound(context, "Webhook");
        var delivery = new WebhookDelivery
        {
            TenantId = item.TenantId,
            SubscriptionId = item.Id,
            EventType = "test",
            EventKey = $"test:{Guid.NewGuid():N}",
            PayloadJson = JsonSerializer.Serialize(new { type = "test", occurredAt = DateTimeOffset.UtcNow }),
        };
        dbContext.WebhookDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Accepted((string?)null, new { delivery.Id });
    }

    private static async Task<IResult> ListWebhookDeliveriesAsync(HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var items = await dbContext.WebhookDeliveries.AsNoTracking().OrderByDescending(entity => entity.CreatedAt).Take(200)
            .Select(entity => new WebhookDeliveryResponse(entity.Id, entity.SubscriptionId, entity.EventType,
                entity.Status, entity.Attempts, entity.LastStatusCode, entity.LastError, entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<WebhookDeliveryResponse>>(items);
    }

    private static async Task<IResult> ListConnectionsAsync(HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var items = await dbContext.IntegrationConnections.AsNoTracking().OrderBy(entity => entity.Name)
            .Select(entity => new ConnectionResponse(entity.Id, entity.Provider, entity.Name, entity.Status,
                entity.LastCheckedAt, entity.LastError, entity.RetryAttempts, entity.NextRetryAt, entity.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<ConnectionResponse>>(items);
    }

    private static async Task<IResult> UpsertConnectionAsync(
        UpsertConnectionRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        IDataProtectionProvider protectionProvider,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Name) ||
            request.Configuration.ValueKind != JsonValueKind.Object)
        {
            return ApiProblems.Validation(context, "A provider, name, and configuration object are required.");
        }

        var authorization = context.TenantAuthorization()!;
        var entity = await dbContext.IntegrationConnections.SingleOrDefaultAsync(
            item => item.Provider == request.Provider.Trim() && item.Name == request.Name.Trim(), cancellationToken);
        entity ??= new IntegrationConnection { TenantId = authorization.TenantId };
        entity.Provider = request.Provider.Trim();
        entity.Name = request.Name.Trim();
        entity.ProtectedConfiguration = protectionProvider.CreateProtector("ReqNest.Integrations.v1")
            .Protect(request.Configuration.GetRawText());
        entity.Status = IntegrationConnectionStatus.Disabled;
        if (dbContext.Entry(entity).State == EntityState.Detached) dbContext.IntegrationConnections.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new ConnectionResponse(entity.Id, entity.Provider, entity.Name, entity.Status,
            entity.LastCheckedAt, entity.LastError, entity.RetryAttempts, entity.NextRetryAt, entity.CreatedAt));
    }

    private static async Task<IResult> TestConnectionAsync(
        Guid connectionId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var entity = await dbContext.IntegrationConnections.SingleOrDefaultAsync(item => item.Id == connectionId, cancellationToken);
        if (entity is null) return ApiProblems.NotFound(context, "Integration connection");
        entity.Status = IntegrationConnectionStatus.Disabled;
        entity.RetryAttempts = 0;
        entity.NextRetryAt = DateTimeOffset.UtcNow;
        entity.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Accepted((string?)null, new ConnectionResponse(entity.Id, entity.Provider, entity.Name, entity.Status,
            entity.LastCheckedAt, entity.LastError, entity.RetryAttempts, entity.NextRetryAt, entity.CreatedAt));
    }

    private static async Task<IResult> GetSsoAsync(HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var item = await dbContext.TenantSsoConfigurations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return TypedResults.Ok(item is null
            ? new SsoResponse(null, "", "", [], false, false, false)
            : new SsoResponse(item.Id, item.Authority, item.ClientId, item.AllowedEmailDomains,
                item.IsEnabled, item.RequireSso, !string.IsNullOrWhiteSpace(item.ProtectedClientSecret)));
    }

    private static async Task<IResult> UpdateSsoAsync(
        UpdateSsoRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        IDataProtectionProvider protectionProvider,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        if (!Uri.TryCreate(request.Authority, UriKind.Absolute, out var authority) || authority.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(request.ClientId) || request.AllowedEmailDomains.Any(domain => !domain.Contains('.')))
        {
            return ApiProblems.Validation(context, "A valid HTTPS authority, client ID, and email domains are required.");
        }

        var authorization = context.TenantAuthorization()!;
        var item = await dbContext.TenantSsoConfigurations.SingleOrDefaultAsync(cancellationToken) ??
                   new TenantSsoConfiguration { TenantId = authorization.TenantId };
        item.Authority = authority.ToString().TrimEnd('/');
        item.ClientId = request.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            item.ProtectedClientSecret = protectionProvider.CreateProtector("ReqNest.Oidc.v1").Protect(request.ClientSecret);
        }
        item.AllowedEmailDomains = request.AllowedEmailDomains.Select(domain => domain.Trim().ToLowerInvariant()).Distinct().ToArray();
        item.IsEnabled = request.IsEnabled;
        item.RequireSso = request.RequireSso;
        if (dbContext.Entry(item).State == EntityState.Detached) dbContext.TenantSsoConfigurations.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> TestSsoAsync(
        HttpContext context,
        ReqNestDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var item = await dbContext.TenantSsoConfigurations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (item is null) return ApiProblems.NotFound(context, "SSO configuration");
        var response = await httpClientFactory.CreateClient().GetAsync(
            item.Authority.TrimEnd('/') + "/.well-known/openid-configuration", cancellationToken);
        return response.IsSuccessStatusCode
            ? TypedResults.Ok(new { discovered = true })
            : ApiProblems.Conflict(context, "OIDC discovery failed.", "oidc_discovery_failed");
    }

    private static async Task<IResult> GetAiAsync(HttpContext context, ReqNestDbContext dbContext, CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        var item = await dbContext.AiTenantConfigurations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return TypedResults.Ok(item is null
            ? new AiConfigurationResponse(false, "None", [], true, false, false, false, "safe-draft-v1")
            : new AiConfigurationResponse(item.IsEnabled, item.Provider, item.AllowedKinds,
                item.RequireHumanReview, item.AllowAttachmentContent, item.ProtectedCredential is not null,
                item.NonTrainingAssuranceAcceptedAt is not null, item.EvaluationVersion));
    }

    private static async Task<IResult> UpdateAiAsync(
        UpdateAiConfigurationRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        IDataProtectionProvider protectionProvider,
        CancellationToken cancellationToken)
    {
        var error = RequireAdministrator(context);
        if (error is not null) return error;
        if (request.IsEnabled && (request.AllowedKinds.Count == 0 || !request.RequireHumanReview || !request.ProviderDoesNotTrain))
        {
            return ApiProblems.Validation(context, "AI requires allowed tasks, mandatory human review, and a non-training provider assurance.");
        }

        var authorization = context.TenantAuthorization()!;
        var item = await dbContext.AiTenantConfigurations.SingleOrDefaultAsync(cancellationToken) ??
                   new AiTenantConfiguration { TenantId = authorization.TenantId };
        item.IsEnabled = request.IsEnabled;
        item.Provider = request.Provider.Trim();
        item.AllowedKinds = request.AllowedKinds.Distinct().ToArray();
        item.RequireHumanReview = true;
        item.AllowAttachmentContent = request.AllowAttachmentContent;
        item.NonTrainingAssuranceAcceptedAt = request.ProviderDoesNotTrain ? DateTimeOffset.UtcNow : null;
        item.EvaluationVersion = "safe-draft-v1";
        if (!string.IsNullOrWhiteSpace(request.Credential))
        {
            item.ProtectedCredential = protectionProvider.CreateProtector("ReqNest.AI.v1").Protect(request.Credential);
        }
        if (dbContext.Entry(item).State == EntityState.Detached) dbContext.AiTenantConfigurations.Add(item);
        AddAudit(dbContext, context, authorization.TenantId, "ai.configuration.updated", item.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static bool ValidWebhook(UpsertWebhookRequest request, out Uri? uri)
    {
        var validUri = Uri.TryCreate(request.Url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                       !uri.IsLoopback;
        return validUri && !string.IsNullOrWhiteSpace(request.Name) &&
               request.EventTypes.Count > 0 && request.EventTypes.All(WebhookEvents.Contains);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void AddAudit(ReqNestDbContext dbContext, HttpContext context, Guid tenantId, string action, Guid id) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = "Integration",
            TargetId = id.ToString(),
            Summary = "Integration configuration changed.",
            CorrelationId = context.TraceIdentifier,
        });
}

public sealed record UpsertEmailChannelRequest(Guid ProjectId, string Address, string DefaultTypeKey, string DefaultPriorityKey, bool IsActive);
public sealed record EmailChannelResponse(Guid Id, Guid ProjectId, string Address, string DefaultTypeKey, string DefaultPriorityKey, bool IsActive, DateTimeOffset CreatedAt);
public sealed record EmailChannelCreatedResponse(EmailChannelResponse Channel, string RawSecret);
public sealed record UpsertWebhookRequest(string Name, string Url, IReadOnlyCollection<string> EventTypes, bool IsActive);
public sealed record WebhookResponse(Guid Id, string Name, string Url, IReadOnlyCollection<string> EventTypes, bool IsActive, DateTimeOffset CreatedAt);
public sealed record WebhookCreatedResponse(WebhookResponse Webhook, string RawSecret);
public sealed record WebhookDeliveryResponse(Guid Id, Guid SubscriptionId, string EventType, WebhookDeliveryStatus Status, int Attempts, int? LastStatusCode, string? LastError, DateTimeOffset CreatedAt);
public sealed record UpsertConnectionRequest(string Provider, string Name, JsonElement Configuration);
public sealed record ConnectionResponse(Guid Id, string Provider, string Name, IntegrationConnectionStatus Status, DateTimeOffset? LastCheckedAt, string? LastError, int RetryAttempts, DateTimeOffset? NextRetryAt, DateTimeOffset CreatedAt);
public sealed record UpdateSsoRequest(string Authority, string ClientId, string? ClientSecret, IReadOnlyCollection<string> AllowedEmailDomains, bool IsEnabled, bool RequireSso);
public sealed record SsoResponse(Guid? Id, string Authority, string ClientId, IReadOnlyCollection<string> AllowedEmailDomains, bool IsEnabled, bool RequireSso, bool HasClientSecret);
public sealed record UpdateAiConfigurationRequest(bool IsEnabled, string Provider, string? Credential, IReadOnlyCollection<AiAssistanceKind> AllowedKinds, bool RequireHumanReview, bool AllowAttachmentContent, bool ProviderDoesNotTrain);
public sealed record AiConfigurationResponse(bool IsEnabled, string Provider, IReadOnlyCollection<AiAssistanceKind> AllowedKinds, bool RequireHumanReview, bool AllowAttachmentContent, bool HasCredential, bool ProviderDoesNotTrain, string EvaluationVersion);
