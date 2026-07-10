using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReqNest.Api.Endpoints;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Tests.Api;

[Collection(ApiCollection.Name)]
public sealed class PhaseThreeExternalServiceTests(ReqNestApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Portal_email_api_webhooks_knowledge_and_ai_work_together()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminClient = factory.CreateClient();
        var session = await RegisterAsync(adminClient, $"external-{suffix}@example.test", $"External {suffix}", $"E{suffix[..5]}");
        UseSession(adminClient, session);
        var tenantId = session.Tenants.Single().TenantId;
        var project = await ReadAsync<ProjectResponse>(await adminClient.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest($"X{suffix[..5]}", "Customer support", "Soutien client", "External service", null, TicketPriority.Normal, null),
            CancellationToken));

        (await adminClient.PutAsJsonAsync("/api/portal/settings", new UpdatePortalSettingsRequest(true, "Welcome", "Bienvenue"), CancellationToken)).EnsureSuccessStatusCode();
        (await adminClient.PutAsJsonAsync($"/api/portal/projects/{project.Id}", new UpdatePortalProjectRequest(true), CancellationToken)).EnsureSuccessStatusCode();
        var publicClient = factory.CreateClient();
        var portal = await ReadAsync<PublicPortalResponse>(await publicClient.GetAsync($"/api/public/portal/{tenantId}", CancellationToken));
        Assert.Equal("Bienvenue", portal.IntroductionFrench);
        Assert.Single(portal.Projects);
        var created = await ReadAsync<RequesterTicketCreatedResponse>(await publicClient.PostAsJsonAsync(
            $"/api/public/portal/{tenantId}/tickets",
            new SubmitRequesterTicketRequest(project.Id, "External Requester", $"requester-{suffix}@example.test", AppLanguage.French,
                "Portal request", "<p>Need <strong>help</strong><script>bad()</script></p>", null, null), CancellationToken));
        Assert.Equal(64, created.AccessToken.Length);
        publicClient.DefaultRequestHeaders.Add("X-Requester-Token", created.AccessToken);
        var status = await ReadAsync<RequesterTicketWithCommentsResponse>(await publicClient.GetAsync($"/api/public/portal/tickets/{created.TicketId}", CancellationToken));
        Assert.DoesNotContain("script", status.Ticket.Description, StringComparison.OrdinalIgnoreCase);
        (await publicClient.PostAsJsonAsync($"/api/public/portal/tickets/{created.TicketId}/comments", new AddRequesterCommentRequest("<p>More detail</p>"), CancellationToken)).EnsureSuccessStatusCode();

        var article = await ReadAsync<KnowledgeArticleResponse>(await adminClient.PostAsJsonAsync(
            "/api/knowledge",
            new UpsertKnowledgeArticleRequest(null, $"reset-{suffix}", "Reset access", "Réinitialiser l'accès", "<p>Follow the steps.</p>", "<p>Suivez les étapes.</p>", KnowledgeArticleVisibility.Requesters),
            CancellationToken));
        (await adminClient.PostAsJsonAsync($"/api/knowledge/{article.Id}/status", new SetKnowledgeStatusRequest(KnowledgeArticleStatus.Published), CancellationToken)).EnsureSuccessStatusCode();
        var publicArticles = await ReadAsync<IReadOnlyCollection<KnowledgeArticleResponse>>(await publicClient.GetAsync($"/api/public/portal/{tenantId}/knowledge?search=reset", CancellationToken));
        Assert.Contains(publicArticles, item => item.Id == article.Id);

        var apiToken = await ReadAsync<ApiTokenCreatedResponse>(await adminClient.PostAsJsonAsync(
            "/api/api-tokens",
            new CreateApiTokenRequest("Ticket automation", [AppPermission.ProjectRead, AppPermission.TicketMaintain], [project.Id], DateTimeOffset.UtcNow.AddDays(30)),
            CancellationToken));
        Assert.StartsWith("rqn_", apiToken.RawToken, StringComparison.Ordinal);
        var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.RawToken);
        tokenClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        (await tokenClient.GetAsync("/api/tickets", CancellationToken)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await tokenClient.PostAsJsonAsync("/api/api-tokens", new CreateApiTokenRequest("Escalation", [AppPermission.AuditView], [], null), CancellationToken)).StatusCode);

        var webhook = await ReadAsync<WebhookCreatedResponse>(await adminClient.PostAsJsonAsync(
            "/api/integrations/webhooks",
            new UpsertWebhookRequest("Audit sink", "https://hooks.example.test/reqnest", ["ticket.created", "ticket.commented"], true), CancellationToken));
        Assert.Equal(64, webhook.RawSecret.Length);
        await ReadAsync<TicketDetailResponse>(await adminClient.PostAsJsonAsync(
            "/api/tickets", new CreateTicketRequest(project.Id, "Webhook event", "<p>Queue delivery</p>", TicketType.Task, TicketPriority.Normal, null, [], null), CancellationToken));
        var deliveries = await ReadAsync<IReadOnlyCollection<WebhookDeliveryResponse>>(await adminClient.GetAsync("/api/integrations/webhooks/deliveries", CancellationToken));
        Assert.Contains(deliveries, item => item.EventType == "ticket.created" && item.Status == WebhookDeliveryStatus.Pending);

        var channel = await ReadAsync<EmailChannelCreatedResponse>(await adminClient.PostAsJsonAsync(
            "/api/integrations/inbound-email",
            new UpsertEmailChannelRequest(project.Id, $"support-{suffix}@example.test", "ServiceRequest", "Normal", true), CancellationToken));
        var mailClient = factory.CreateClient();
        mailClient.DefaultRequestHeaders.Add("X-Inbound-Secret", channel.RawSecret);
        var messageId = $"<{suffix}-1@example.test>";
        var inbound = await mailClient.PostAsJsonAsync($"/api/public/inbound-email/{channel.Channel.Id}", new
        {
            messageId,
            inReplyTo = (string?)null,
            senderEmail = $"mail-{suffix}@example.test",
            senderName = "Mail Requester",
            subject = "Email request",
            body = "<p>Created by email.</p>",
            language = "English",
            autoSubmitted = false,
            attachments = new[] { new { fileName = "notes.txt", contentBase64 = Convert.ToBase64String("safe text"u8) } },
        }, CancellationToken);
        inbound.EnsureSuccessStatusCode();
        var inboundJson = await inbound.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: CancellationToken);
        var emailTicketId = inboundJson.GetProperty("id").GetGuid();
        var duplicate = await mailClient.PostAsJsonAsync($"/api/public/inbound-email/{channel.Channel.Id}", new
        {
            messageId,
            senderEmail = $"mail-{suffix}@example.test",
            subject = "Email request",
            body = "duplicate",
            language = "English",
            autoSubmitted = false,
        }, CancellationToken);
        Assert.Equal("duplicate", (await duplicate.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: CancellationToken)).GetProperty("status").GetString());
        var reply = await mailClient.PostAsJsonAsync($"/api/public/inbound-email/{channel.Channel.Id}", new
        {
            messageId = $"<{suffix}-2@example.test>",
            inReplyTo = messageId,
            senderEmail = $"mail-{suffix}@example.test",
            subject = "Re: Email request",
            body = "<p>Reply detail</p>",
            language = "English",
            autoSubmitted = false,
        }, CancellationToken);
        reply.EnsureSuccessStatusCode();
        Assert.True((await reply.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: CancellationToken)).GetProperty("threaded").GetBoolean());
        var loop = await mailClient.PostAsJsonAsync($"/api/public/inbound-email/{channel.Channel.Id}", new
        {
            messageId = $"<{suffix}-3@example.test>",
            senderEmail = channel.Channel.Address,
            subject = "Loop",
            body = "loop",
            language = "English",
            autoSubmitted = true,
        }, CancellationToken);
        Assert.Equal("rejected", (await loop.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: CancellationToken)).GetProperty("status").GetString());

        (await adminClient.PutAsJsonAsync("/api/integrations/ai", new UpdateAiConfigurationRequest(true, "ReqNestSafeDraft", null,
            [AiAssistanceKind.Summarize, AiAssistanceKind.SuggestReply], true, false, true), CancellationToken)).EnsureSuccessStatusCode();
        var draft = await ReadAsync<AiAssistanceResponse>(await adminClient.PostAsJsonAsync(
            $"/api/tickets/{emailTicketId}/ai-assistance", new CreateAiAssistanceRequest(AiAssistanceKind.Summarize), CancellationToken));
        Assert.Equal(AiAssistanceStatus.Draft, draft.Status);
        Assert.Equal(1m, draft.EvaluationScore);
        var reviewed = await ReadAsync<AiAssistanceResponse>(await adminClient.PostAsJsonAsync(
            $"/api/tickets/{emailTicketId}/ai-assistance/{draft.Id}/review", new ReviewAiAssistanceRequest(true), CancellationToken));
        Assert.Equal(AiAssistanceStatus.Accepted, reviewed.Status);
    }

    [Fact]
    public async Task Sso_and_connector_credentials_are_protected_and_tenant_isolated()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = factory.CreateClient();
        var session = await RegisterAsync(client, $"security-{suffix}@example.test", $"Security {suffix}", $"S{suffix[..5]}");
        UseSession(client, session);
        const string oidcSecret = "oidc-super-secret-value";
        (await client.PutAsJsonAsync("/api/integrations/sso", new UpdateSsoRequest(
            "https://login.example.test/tenant", "reqnest-client", oidcSecret, ["example.test"], true, false), CancellationToken)).EnsureSuccessStatusCode();
        const string bearer = "connector-super-secret-value";
        await ReadAsync<ConnectionResponse>(await client.PostAsJsonAsync("/api/integrations/connections", new
        {
            provider = "GenericHttp",
            name = "CRM",
            configuration = new { healthCheckUrl = "https://api.example.test/health", bearerToken = bearer },
        }, CancellationToken));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var tenantId = session.Tenants.Single().TenantId;
        var sso = await db.TenantSsoConfigurations.IgnoreQueryFilters().SingleAsync(item => item.TenantId == tenantId, CancellationToken);
        var connector = await db.IntegrationConnections.IgnoreQueryFilters().SingleAsync(item => item.TenantId == tenantId, CancellationToken);
        Assert.DoesNotContain(oidcSecret, sso.ProtectedClientSecret, StringComparison.Ordinal);
        Assert.DoesNotContain(bearer, connector.ProtectedConfiguration, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/public/portal/{tenantId}", CancellationToken)).StatusCode);
    }

    private static async Task<AuthenticatedSession> RegisterAsync(HttpClient client, string email, string company, string shortName) =>
        await ReadAsync<AuthenticatedSession>(await client.PostAsJsonAsync("/api/auth/register-tenant",
            new RegisterTenantRequest(company, shortName, company + " Admin", email, "A-secure-test-password-2026!", AppLanguage.English, "Africa/Johannesburg"), CancellationToken));
    private static void UseSession(HttpClient client, AuthenticatedSession session)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", session.Tenants.Single().TenantId.ToString());
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(CancellationToken));
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, CancellationToken))!;
    }
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
