using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReqNest.Api.Background;
using ReqNest.Api.Endpoints;
using ReqNest.Core.Configuration;
using ReqNest.Core.Identity;
using ReqNest.Core.Reports;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Tests.Api;

[Collection(ApiCollection.Name)]
public sealed class PhaseTwoOperationalMaturityTests(ReqNestApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Configurable_schema_sla_relationships_bulk_reports_and_retention_work_together()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = factory.CreateClient();
        var session = await RegisterAsync(client, $"operations-{suffix}@example.test", $"Operations {suffix}", $"O{suffix[..5]}");
        UseSession(client, session);
        var project = await CreateProjectAsync(client, $"S{suffix[..5]}");

        var type = await ReadAsync<TicketTypeDefinitionResponse>(await client.PostAsJsonAsync(
            "/api/configuration/ticket-schema/types",
            new UpsertTicketTypeDefinitionRequest(project.Id, "CHANGE", "Change", 10, true),
            CancellationToken));
        var priority = await ReadAsync<TicketPriorityDefinitionResponse>(await client.PostAsJsonAsync(
            "/api/configuration/ticket-schema/priorities",
            new UpsertTicketPriorityDefinitionRequest(project.Id, "P1", "Critical", "#DC2626", 100, 10, true),
            CancellationToken));
        var field = await ReadAsync<CustomFieldDefinitionResponse>(await client.PostAsJsonAsync(
            "/api/configuration/ticket-schema/custom-fields",
            new UpsertCustomFieldDefinitionRequest(
                [project.Id],
                "ENVIRONMENT",
                "Environment",
                CustomFieldKind.Choice,
                true,
                true,
                10,
                JsonSerializer.SerializeToElement(new[] { "production", "staging" })),
            CancellationToken));
        Assert.Equal("CHANGE", type.Key);
        Assert.Equal("P1", priority.Key);
        Assert.True(field.IsRequired);

        var sla = await ReadAsync<SlaPolicyResponse>(await client.PostAsJsonAsync(
            "/api/configuration/sla-policies",
            new UpsertSlaPolicyRequest(
                [project.Id],
                "Critical support",
                "Africa/Johannesburg",
                false,
                true,
                62,
                8 * 60,
                17 * 60,
                30,
                ["WAITING"],
                [new SlaTargetRequest("P1", 30, 120)],
                [new SlaHolidayRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), "Company holiday")]),
            CancellationToken));
        (await client.PutAsync($"/api/configuration/sla-policies/{sla.Id}/projects/{project.Id}", null, CancellationToken))
            .EnsureSuccessStatusCode();

        var customFields = new Dictionary<string, JsonElement>
        {
            ["ENVIRONMENT"] = JsonSerializer.SerializeToElement("production"),
        };
        var first = await CreateTicketAsync(client, project.Id, "Critical deployment", customFields);
        var second = await CreateTicketAsync(client, project.Id, "Follow-up deployment", customFields);
        Assert.Equal("CHANGE", first.TypeKey);
        Assert.Equal("P1", first.PriorityKey);
        Assert.Equal("production", first.CustomFields["ENVIRONMENT"].GetString());
        Assert.Equal("Critical support", first.SlaPolicyName);
        Assert.NotNull(first.ResolutionTargetAt);
        Assert.NotNull(first.SlaWarningAt);

        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        };
        using var imageUpload = new ByteArrayContent(pngBytes);
        imageUpload.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        imageUpload.Headers.Add("X-File-Name", "preview.png");
        var attachment = await ReadAsync<AttachmentResponse>(await client.PostAsync(
            $"/api/tickets/{first.Id}/attachments",
            imageUpload,
            CancellationToken));
        var previewResponse = await client.GetAsync(
            $"/api/attachments/{attachment.Id}/preview",
            CancellationToken);
        previewResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", previewResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "default-src 'none'; img-src 'self' data:; style-src 'none'; sandbox",
            previewResponse.Headers.GetValues("Content-Security-Policy").Single());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
            var tenantId = session.Tenants.Single().TenantId;
            var tenant = await dbContext.Tenants.IgnoreQueryFilters().SingleAsync(
                entity => entity.Id == tenantId,
                CancellationToken);
            tenant.StorageQuotaBytes = pngBytes.Length;
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        using var overQuotaUpload = new ByteArrayContent("too-large"u8.ToArray());
        overQuotaUpload.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        overQuotaUpload.Headers.Add("X-File-Name", "over-quota.txt");
        var overQuotaResponse = await client.PostAsync(
            $"/api/tickets/{first.Id}/attachments",
            overQuotaUpload,
            CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, overQuotaResponse.StatusCode);
        Assert.Contains(
            "storage_quota_exceeded",
            await overQuotaResponse.Content.ReadAsStringAsync(CancellationToken),
            StringComparison.Ordinal);

        var relationship = await ReadAsync<TicketRelationshipResponse>(await client.PostAsJsonAsync(
            $"/api/tickets/{first.Id}/relationships",
            new CreateTicketRelationshipRequest(second.Id, TicketRelationshipType.Blocks),
            CancellationToken));
        Assert.Equal(second.Id, relationship.RelatedTicketId);
        (await client.PutAsJsonAsync(
            $"/api/tickets/{second.Id}/parent",
            new SetParentTicketRequest(first.Id),
            CancellationToken)).EnsureSuccessStatusCode();
        var cycle = await client.PutAsJsonAsync(
            $"/api/tickets/{first.Id}/parent",
            new SetParentTicketRequest(second.Id),
            CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, cycle.StatusCode);

        var bulk = new BulkTicketUpdateRequest(
            [first.Id, second.Id, Guid.NewGuid()],
            TicketPriority.Urgent,
            false,
            null,
            null,
            null,
            "P1");
        var preview = await ReadAsync<BulkTicketUpdateResponse>(await client.PostAsJsonAsync(
            "/api/tickets/bulk/preview",
            bulk,
            CancellationToken));
        Assert.Equal(2, preview.Updated);
        Assert.Single(preview.Failures);
        var applied = await ReadAsync<BulkTicketUpdateResponse>(await client.PostAsJsonAsync(
            "/api/tickets/bulk",
            bulk,
            CancellationToken));
        Assert.Equal(2, applied.Updated);

        var csv = await client.GetAsync($"/api/reports/inventory/csv?projectId={project.Id}&language=French", CancellationToken);
        csv.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Nombre", await csv.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
        var retention = await ReadAsync<RetentionSettingsResponse>(await client.GetAsync(
            "/api/operations/retention",
            CancellationToken));
        Assert.True(retention.StorageUsedBytes >= 0);
        var auditCsv = await client.GetAsync("/api/audit/export.csv", CancellationToken);
        auditCsv.EnsureSuccessStatusCode();
        Assert.Contains("ticket.relationship.created", await auditCsv.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Published_views_custom_roles_and_email_outbox_respect_project_scope()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminClient = factory.CreateClient();
        var admin = await RegisterAsync(adminClient, $"roles-{suffix}@example.test", $"Roles {suffix}", $"R{suffix[..5]}");
        UseSession(adminClient, admin);
        var project = await CreateProjectAsync(adminClient, $"C{suffix[..5]}");
        var hiddenProject = await CreateProjectAsync(adminClient, $"H{suffix[..5]}");
        var ticket = await ReadAsync<TicketDetailResponse>(await adminClient.PostAsJsonAsync(
            "/api/tickets",
            new CreateTicketRequest(project.Id, "Scoped change", "<p>Initial</p>", TicketType.Task, TicketPriority.Normal, null, [], null),
            CancellationToken));

        await ReadAsync<SavedViewResponse>(await adminClient.PostAsJsonAsync(
            "/api/saved-views",
            new SaveViewRequest(
                "Project triage",
                project.Id,
                JsonSerializer.SerializeToElement(new { projectId = project.Id }),
                JsonSerializer.SerializeToElement(new { updatedAt = "desc" }),
                JsonSerializer.SerializeToElement(new[] { "key", "title" }),
                null,
                true),
            CancellationToken));
        var customRole = await ReadAsync<CustomRoleResponse>(await adminClient.PostAsJsonAsync(
            "/api/custom-roles",
            new UpsertCustomRoleRequest(
                "Ticket helper",
                "Can maintain assigned project tickets",
                [AppPermission.ProjectRead, AppPermission.TicketMaintain, AppPermission.CommentAdd],
                true),
            CancellationToken));

        var observerEmail = $"helper-{suffix}@example.test";
        var invitation = await ReadAsync<InvitationCreatedResponse>(await adminClient.PostAsJsonAsync(
            "/api/members/invitations",
            new InviteMemberRequest(observerEmail, "Project Helper", [new RoleGrantRequest(AppRole.Observer, false, [project.Id])]),
            CancellationToken));
        var observerClient = factory.CreateClient();
        (await observerClient.PostAsJsonAsync(
            "/api/auth/accept-invitation",
            new AcceptInvitationRequest(invitation.DevelopmentToken!, "Project Helper", "Helper-secure-password-2026!"),
            CancellationToken)).EnsureSuccessStatusCode();
        var members = await ReadAsync<IReadOnlyCollection<MemberResponse>>(await adminClient.GetAsync("/api/members", CancellationToken));
        var membership = members.Single(item => item.Email == observerEmail);
        (await adminClient.PutAsJsonAsync(
            $"/api/members/{membership.MembershipId}/custom-role-grants",
            new UpdateCustomRoleGrantsRequest([new CustomRoleGrantRequest(customRole.Id, false, [project.Id])]),
            CancellationToken)).EnsureSuccessStatusCode();

        var observer = await LoginAsync(observerClient, observerEmail, "Helper-secure-password-2026!");
        Assert.Contains(
            AppPermission.TicketMaintain,
            observer.Tenants.Single().ProjectPermissions[project.Id]);
        UseSession(observerClient, observer);
        var visibleViews = await ReadAsync<IReadOnlyCollection<SavedViewResponse>>(await observerClient.GetAsync(
            "/api/saved-views",
            CancellationToken));
        Assert.Contains(visibleViews, view => view.Name == "Project triage" && view.IsPublished);
        Assert.Equal(HttpStatusCode.NotFound, (await observerClient.GetAsync($"/api/projects/{hiddenProject.Id}", CancellationToken)).StatusCode);

        var updated = await observerClient.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}",
            new UpdateTicketRequest(
                "Scoped change updated",
                ticket.Description,
                ticket.Type,
                ticket.Priority,
                ticket.AssigneeUserId,
                ticket.Labels,
                ticket.DueAt,
                null,
                ticket.Version),
            CancellationToken);
        updated.EnsureSuccessStatusCode();

        await ReadAsync<NotificationPreferencesResponse>(await adminClient.PutAsJsonAsync(
            "/api/notifications/preferences",
            new NotificationPreferencesResponse(true, true, true, false, true, 8),
            CancellationToken));
        (await observerClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new CreateCommentRequest("<p>Please review.</p>", []),
            CancellationToken)).EnsureSuccessStatusCode();
        var outbox = await ReadAsync<PagedEmailOutboxResponse>(await adminClient.GetAsync(
            "/api/operations/email-outbox",
            CancellationToken));
        Assert.Contains(outbox.Items, message => message.RecipientEmail.Contains("roles-", StringComparison.Ordinal));
    }

    private static async Task<TicketDetailResponse> CreateTicketAsync(
        HttpClient client,
        Guid projectId,
        string title,
        IReadOnlyDictionary<string, JsonElement> customFields) =>
        await ReadAsync<TicketDetailResponse>(await client.PostAsJsonAsync(
            "/api/tickets",
            new CreateTicketRequest(
                projectId,
                title,
                "<p>Operational ticket</p>",
                TicketType.Task,
                TicketPriority.Urgent,
                null,
                [],
                null,
                "CHANGE",
                "P1",
                customFields),
            CancellationToken));

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string key) =>
        await ReadAsync<ProjectResponse>(await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest(key, "Operations", "Phase two project", null, TicketPriority.Normal, null),
            CancellationToken));

    private static async Task<AuthenticatedSession> RegisterAsync(HttpClient client, string email, string company, string shortName) =>
        await ReadAsync<AuthenticatedSession>(await client.PostAsJsonAsync(
            "/api/auth/register-tenant",
            new RegisterTenantRequest(
                company,
                shortName,
                company + " Admin",
                email,
                "A-secure-test-password-2026!",
                AppLanguage.English,
                "Africa/Johannesburg"),
            CancellationToken));

    private static async Task<AuthenticatedSession> LoginAsync(HttpClient client, string email, string password) =>
        await ReadAsync<AuthenticatedSession>(await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password),
            CancellationToken));

    private static void UseSession(HttpClient client, AuthenticatedSession session)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
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
