using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReqNest.Api.Endpoints;
using ReqNest.Core.Identity;
using ReqNest.Core.Tickets;
using ReqNest.Core.Workflows;
using ReqNest.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ReqNest.Tests.Api;

[Collection(ApiCollection.Name)]
public sealed class PhaseOneCoreEndpointsTests(ReqNestApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Project_ticket_workflow_comment_scope_and_report_journey_is_enforced()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminClient = factory.CreateClient();
        var admin = await RegisterAsync(adminClient, $"admin-{suffix}@example.test", $"Company {suffix}", $"C{suffix[..5]}");
        UseSession(adminClient, admin);

        var project = await CreateProjectAsync(adminClient, $"P{suffix[..5]}", "Operations", "Opérations");
        var otherProject = await CreateProjectAsync(adminClient, $"Q{suffix[..5]}", "Web", "Web");

        var createTicket = new CreateTicketRequest(
            project.Id,
            "Printer unavailable",
            "<p onclick=\"bad()\">Please <strong>help</strong><script>alert(1)</script><a href=\"javascript:bad()\">now</a></p>",
            TicketType.Incident,
            TicketPriority.High,
            null,
            ["office", "Printer"],
            DateTimeOffset.UtcNow.AddDays(1));
        adminClient.DefaultRequestHeaders.Add("Idempotency-Key", $"ticket-{suffix}");
        var createResponse = await adminClient.PostAsJsonAsync("/api/tickets", createTicket, TestCancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var ticket = await ReadAsync<TicketDetailResponse>(createResponse);
        Assert.StartsWith(project.Key + "-", ticket.Key, StringComparison.Ordinal);
        Assert.Contains("<strong>help</strong>", ticket.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("script", ticket.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", ticket.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", ticket.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["office", "printer"], ticket.Labels);

        var duplicate = await adminClient.PostAsJsonAsync("/api/tickets", createTicket, TestCancellationToken);
        duplicate.EnsureSuccessStatusCode();
        Assert.Equal(ticket.Id, (await ReadAsync<TicketDetailResponse>(duplicate)).Id);
        adminClient.DefaultRequestHeaders.Remove("Idempotency-Key");

        var workflows = await ReadAsync<IReadOnlyCollection<WorkflowResponse>>(
            await adminClient.GetAsync("/api/workflows", TestCancellationToken));
        var workflow = workflows.Single(entity => entity.Id == project.WorkflowId);
        var inProgress = workflow.Statuses.Single(entity => entity.Key == "IN_PROGRESS");
        var done = workflow.Statuses.Single(entity => entity.Key == "DONE");
        var invalidTransition = await adminClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/transition",
            new TransitionTicketRequest(done.Id, null, ticket.Version),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
            var persistedVersion = await dbContext.Tickets.IgnoreQueryFilters()
                .Where(entity => entity.Id == ticket.Id)
                .Select(entity => entity.Version)
                .SingleAsync(TestCancellationToken);
            Assert.Equal(persistedVersion, ticket.Version);
        }

        var transition = await adminClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/transition",
            new TransitionTicketRequest(inProgress.Id, null, ticket.Version),
            TestCancellationToken);
        Assert.True(
            transition.IsSuccessStatusCode,
            await transition.Content.ReadAsStringAsync(TestCancellationToken));
        ticket = await ReadAsync<TicketDetailResponse>(transition);
        Assert.Equal("IN_PROGRESS", ticket.StatusKey);

        var staleVersion = ticket.Version;
        var updateRequest = new UpdateTicketRequest(
            "Printer unavailable on floor 2",
            ticket.Description,
            ticket.Type,
            TicketPriority.Urgent,
            ticket.AssigneeUserId,
            ticket.Labels,
            ticket.DueAt,
            null,
            staleVersion);
        var update = await adminClient.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}",
            updateRequest,
            TestCancellationToken);
        update.EnsureSuccessStatusCode();
        var staleUpdate = await adminClient.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}",
            updateRequest,
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        var observerEmail = $"observer-{suffix}@example.test";
        var invitationResponse = await adminClient.PostAsJsonAsync("/api/members/invitations", new InviteMemberRequest(
            observerEmail,
            "Scoped Observer",
            [new RoleGrantRequest(AppRole.Observer, false, [project.Id])]),
            TestCancellationToken);
        invitationResponse.EnsureSuccessStatusCode();
        var invitation = await ReadAsync<InvitationCreatedResponse>(invitationResponse);
        Assert.NotNull(invitation.DevelopmentToken);

        var anonymousClient = factory.CreateClient();
        var acceptance = await anonymousClient.PostAsJsonAsync("/api/auth/accept-invitation", new AcceptInvitationRequest(
            invitation.DevelopmentToken!,
            "Scoped Observer",
            "Observer-secure-password-2026!"),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, acceptance.StatusCode);
        var observer = await LoginAsync(anonymousClient, observerEmail, "Observer-secure-password-2026!");
        UseSession(anonymousClient, observer);

        var visibleProjects = await ReadAsync<IReadOnlyCollection<ProjectResponse>>(
            await anonymousClient.GetAsync("/api/projects", TestCancellationToken));
        Assert.Single(visibleProjects);
        Assert.Equal(project.Id, visibleProjects.Single().Id);
        Assert.DoesNotContain(visibleProjects, entity => entity.Id == otherProject.Id);

        var observerReads = await anonymousClient.GetAsync(
            $"/api/tickets/{ticket.Id}",
            TestCancellationToken);
        observerReads.EnsureSuccessStatusCode();
        var observerUpdate = await anonymousClient.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}",
            updateRequest,
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, observerUpdate.StatusCode);
        var commentResponse = await anonymousClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new CreateCommentRequest("<p>Observed <img src=x onerror=bad()><em>carefully</em>.</p>", []),
            TestCancellationToken);
        commentResponse.EnsureSuccessStatusCode();
        var comment = await ReadAsync<TicketCommentResponse>(commentResponse);
        Assert.DoesNotContain("img", comment.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<em>carefully</em>", comment.Body, StringComparison.Ordinal);

        using var observerAttachmentContent = new ByteArrayContent("observer upload denied"u8.ToArray());
        observerAttachmentContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        observerAttachmentContent.Headers.Add("X-File-Name", "observer-note.txt");
        var observerAttachment = await anonymousClient.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments",
            observerAttachmentContent,
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, observerAttachment.StatusCode);

        var hiddenProject = await anonymousClient.GetAsync(
            $"/api/projects/{otherProject.Id}",
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, hiddenProject.StatusCode);

        var notifications = await ReadAsync<PagedNotificationResponse>(
            await adminClient.GetAsync("/api/notifications?unread=true", TestCancellationToken));
        Assert.Contains(notifications.Items, entity => entity.Type == Core.Notifications.NotificationType.TicketCommented);

        var report = await ReadAsync<ReportResponse>(await adminClient.GetAsync(
            $"/api/reports/inventory?projectId={project.Id}",
            TestCancellationToken));
        Assert.Contains(report.Rows, row => ((JsonElement)row["Count"]!).GetInt32() == 1);
        Assert.Contains(report.DefinitionsEnglish, definition => definition.Contains("current number", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Password_reset_is_single_use_and_revokes_existing_sessions()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"reset-{suffix}@example.test";
        var oldPassword = "Original-secure-password-2026!";
        var newPassword = "Replacement-secure-password-2026!";
        var client = factory.CreateClient();
        var session = await RegisterAsync(client, email, $"Reset {suffix}", $"R{suffix[..5]}", oldPassword);
        UseSession(client, session);

        var resetRequested = await client.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new PasswordResetRequest(email),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, resetRequested.StatusCode);
        var issued = await ReadAsync<PasswordResetRequestedResponse>(resetRequested);
        Assert.NotNull(issued.DevelopmentToken);

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(issued.DevelopmentToken!, newPassword),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var reused = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(issued.DevelopmentToken!, "Another-secure-password-2026!"),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);

        var revokedSession = await client.GetAsync("/api/auth/me", TestCancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSession.StatusCode);
        var oldLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, oldPassword),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, newPassword),
            TestCancellationToken);
        newLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Private_attachment_and_branded_pdf_export_use_authorized_blob_storage()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = factory.CreateClient();
        var admin = await RegisterAsync(client, $"files-{suffix}@example.test", $"Files {suffix}", $"F{suffix[..5]}");
        UseSession(client, admin);
        var project = await CreateProjectAsync(client, $"D{suffix[..5]}", "Documents", "Documents");
        var ticketResponse = await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(
            project.Id,
            "Review screenshot",
            "<p>Review the attached screenshot.</p>",
            TicketType.Task,
            TicketPriority.Normal,
            null,
            [],
            null), TestCancellationToken);
        var ticket = await ReadAsync<TicketDetailResponse>(ticketResponse);

        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        };
        using var uploadContent = new ByteArrayContent(pngBytes);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        uploadContent.Headers.Add("X-File-Name", "evidence.png");
        var upload = await client.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments",
            uploadContent,
            TestCancellationToken);
        var attachment = await ReadAsync<AttachmentResponse>(upload);
        Assert.Equal(AttachmentScanStatus.Clean, attachment.ScanStatus);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pngBytes)), attachment.ChecksumSha256);

        var download = await client.GetAsync($"/api/attachments/{attachment.Id}", TestCancellationToken);
        download.EnsureSuccessStatusCode();
        Assert.Equal(pngBytes, await download.Content.ReadAsByteArrayAsync(TestCancellationToken));
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
            var storedAttachment = await dbContext.Attachments.IgnoreQueryFilters()
                .SingleAsync(entity => entity.Id == attachment.Id, TestCancellationToken);
            storedAttachment.ScanStatus = AttachmentScanStatus.Quarantined;
            await dbContext.SaveChangesAsync(TestCancellationToken);
        }
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.GetAsync($"/api/attachments/{attachment.Id}", TestCancellationToken)).StatusCode);

        using var badContent = new ByteArrayContent("not-an-executable"u8.ToArray());
        badContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        badContent.Headers.Add("X-File-Name", "danger.exe");
        var badUpload = await client.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments",
            badContent,
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, badUpload.StatusCode);

        using var logoImage = new Image<Rgba32>(2, 2, new Rgba32(37, 99, 235));
        using var logoStream = new MemoryStream();
        await logoImage.SaveAsPngAsync(logoStream, TestCancellationToken);
        var logoBytes = logoStream.ToArray();
        using var logoContent = new ByteArrayContent(logoBytes);
        logoContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var logoUpload = await client.PostAsync(
            "/api/tenants/current/logos/light",
            logoContent,
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logoUpload.StatusCode);

        var exportResponse = await client.PostAsJsonAsync("/api/reports/exports", new CreateReportExportRequest(
            "inventory",
            new ReportFilterRequest(project.Id, null, null, null, null, null, false),
            AppLanguage.French), TestCancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, exportResponse.StatusCode);
        var reportExport = await ReadAsync<ReportExportResponse>(exportResponse);
        var pdf = await client.GetAsync(
            $"/api/reports/exports/{reportExport.Id}/download",
            TestCancellationToken);
        pdf.EnsureSuccessStatusCode();
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync(TestCancellationToken);
        Assert.True(pdfBytes.AsSpan().StartsWith("%PDF-1.7"u8));
        Assert.Contains("/Logo", System.Text.Encoding.Latin1.GetString(pdfBytes), StringComparison.Ordinal);
        Assert.Contains("application/pdf", pdf.Content.Headers.ContentType!.MediaType, StringComparison.Ordinal);

        var foreignClient = factory.CreateClient();
        var foreign = await RegisterAsync(
            foreignClient,
            $"foreign-{suffix}@example.test",
            $"Foreign {suffix}",
            $"X{suffix[..5]}");
        UseSession(foreignClient, foreign);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await foreignClient.GetAsync($"/api/attachments/{attachment.Id}", TestCancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await foreignClient.GetAsync(
                $"/api/reports/exports/{reportExport.Id}/download",
                TestCancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Multi_tenant_memberships_and_all_or_selected_project_scopes_follow_future_projects()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sharedEmail = $"shared-{suffix}@example.test";
        var sharedPassword = "Shared-secure-password-2026!";
        var sharedClient = factory.CreateClient();
        var firstTenant = await RegisterAsync(sharedClient, sharedEmail, $"First {suffix}", $"A{suffix[..5]}", sharedPassword);

        var adminClient = factory.CreateClient();
        var secondTenantAdmin = await RegisterAsync(adminClient, $"second-{suffix}@example.test", $"Second {suffix}", $"B{suffix[..5]}");
        UseSession(adminClient, secondTenantAdmin);
        var scopedProject = await CreateProjectAsync(adminClient, $"S{suffix[..5]}", "Scoped", "Limité");
        var hiddenProject = await CreateProjectAsync(adminClient, $"H{suffix[..5]}", "Hidden", "Caché");

        var invitation = await ReadAsync<InvitationCreatedResponse>(await adminClient.PostAsJsonAsync(
            "/api/members/invitations",
            new InviteMemberRequest(sharedEmail, "Shared User", [new RoleGrantRequest(AppRole.Observer, false, [scopedProject.Id])]),
            TestCancellationToken));
        var accept = await sharedClient.PostAsJsonAsync(
            "/api/auth/accept-invitation",
            new AcceptInvitationRequest(invitation.DevelopmentToken!, null, null),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var sharedSession = await LoginAsync(sharedClient, sharedEmail, sharedPassword);
        Assert.Equal(2, sharedSession.Tenants.Count);
        var secondTenantId = secondTenantAdmin.Tenants.Single().TenantId;
        UseSession(sharedClient, sharedSession, secondTenantId);
        var selectedProjects = await ReadAsync<IReadOnlyCollection<ProjectResponse>>(await sharedClient.GetAsync(
            "/api/projects",
            TestCancellationToken));
        Assert.Equal([scopedProject.Id], selectedProjects.Select(project => project.Id));
        Assert.DoesNotContain(selectedProjects, project => project.Id == hiddenProject.Id);

        var members = await ReadAsync<IReadOnlyCollection<MemberResponse>>(await adminClient.GetAsync(
            "/api/members",
            TestCancellationToken));
        var sharedMembership = members.Single(member => member.Email == sharedEmail);
        var allProjectsUpdate = await adminClient.PutAsJsonAsync(
            $"/api/members/{sharedMembership.MembershipId}/roles",
            new UpdateMemberRolesRequest([new RoleGrantRequest(AppRole.Observer, true, [])]),
            TestCancellationToken);
        allProjectsUpdate.EnsureSuccessStatusCode();
        var futureProject = await CreateProjectAsync(adminClient, $"F{suffix[..5]}", "Future", "Futur");

        UseSession(sharedClient, await LoginAsync(sharedClient, sharedEmail, sharedPassword), secondTenantId);
        var allProjects = await ReadAsync<IReadOnlyCollection<ProjectResponse>>(await sharedClient.GetAsync(
            "/api/projects",
            TestCancellationToken));
        Assert.Contains(allProjects, project => project.Id == hiddenProject.Id);
        Assert.Contains(allProjects, project => project.Id == futureProject.Id);

        var adminMembership = members.Single(member => member.UserId == secondTenantAdmin.UserId);
        var lastAdminDeactivation = await adminClient.PostAsync(
            $"/api/members/{adminMembership.MembershipId}/deactivate",
            null,
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, lastAdminDeactivation.StatusCode);
        Assert.Single(firstTenant.Tenants);
    }

    [Fact]
    public async Task Project_workflow_copy_is_isolated_and_used_status_removal_requires_mapping()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = factory.CreateClient();
        var admin = await RegisterAsync(client, $"workflow-{suffix}@example.test", $"Workflow {suffix}", $"W{suffix[..5]}");
        UseSession(client, admin);
        var project = await CreateProjectAsync(client, $"M{suffix[..5]}", "Mapped", "Correspondance");
        var untouchedProject = await CreateProjectAsync(client, $"U{suffix[..5]}", "Untouched", "Inchangé");
        var defaultWorkflow = (await ReadAsync<IReadOnlyCollection<WorkflowResponse>>(await client.GetAsync(
            "/api/workflows",
            TestCancellationToken))).Single(workflow => workflow.IsDefault);
        var copied = await ReadAsync<WorkflowResponse>(await client.PostAsJsonAsync(
            $"/api/workflows/{defaultWorkflow.Id}/copy-to-project/{project.Id}",
            new CopyWorkflowRequest($"Mapped {suffix}"),
            TestCancellationToken));
        var projectsAfterCopy = await ReadAsync<IReadOnlyCollection<ProjectResponse>>(await client.GetAsync(
            "/api/projects",
            TestCancellationToken));
        Assert.Equal(copied.Id, projectsAfterCopy.Single(item => item.Id == project.Id).WorkflowId);
        Assert.Equal(defaultWorkflow.Id, projectsAfterCopy.Single(item => item.Id == untouchedProject.Id).WorkflowId);

        var ticket = await ReadAsync<TicketDetailResponse>(await client.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(
            project.Id, "Map this ticket", "<p>Mapping safety</p>", TicketType.Task, TicketPriority.Normal, null, [], null),
            TestCancellationToken));
        Assert.Equal("TODO", ticket.StatusKey);
        var retained = copied.Statuses.Where(status => status.Key != "TODO").OrderBy(status => status.Order).ToArray();
        var definitions = retained.Select((status, index) => new WorkflowStatusRequest(
            status.Key,
            status.LabelEnglish,
            status.LabelFrench,
            status.Category,
            index,
            status.Color,
            status.Key == "IN_PROGRESS",
            status.IsTerminal)).ToArray();
        var transitions = new[]
        {
            new WorkflowTransitionRequest("IN_PROGRESS", "DONE", null, null, false),
            new WorkflowTransitionRequest("DONE", "IN_PROGRESS", null, null, false),
        };
        var withoutMapping = await client.PutAsJsonAsync($"/api/workflows/{copied.Id}", new UpdateWorkflowRequest(
            copied.Name, copied.Description, true, definitions, transitions, new Dictionary<string, string>()),
            TestCancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, withoutMapping.StatusCode);

        var mapped = await client.PutAsJsonAsync($"/api/workflows/{copied.Id}", new UpdateWorkflowRequest(
            copied.Name, copied.Description, true, definitions, transitions, new Dictionary<string, string> { ["TODO"] = "IN_PROGRESS" }),
            TestCancellationToken);
        mapped.EnsureSuccessStatusCode();
        var mappedTicket = await ReadAsync<TicketDetailResponse>(await client.GetAsync(
            $"/api/tickets/{ticket.Id}",
            TestCancellationToken));
        Assert.Equal("IN_PROGRESS", mappedTicket.StatusKey);
        var untouched = (await ReadAsync<IReadOnlyCollection<WorkflowResponse>>(await client.GetAsync(
            "/api/workflows",
            TestCancellationToken))).Single(workflow => workflow.Id == defaultWorkflow.Id);
        Assert.Equal(["TODO", "IN_PROGRESS", "DONE"], untouched.Statuses.OrderBy(status => status.Order).Select(status => status.Key));
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        string key,
        string nameEnglish,
        string nameFrench)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(
            key,
            nameEnglish,
            nameFrench,
            "Integration test project",
            null,
            TicketPriority.Normal,
            null), TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProjectResponse>(response);
    }

    private static async Task<AuthenticatedSession> RegisterAsync(
        HttpClient client,
        string email,
        string company,
        string shortName,
        string password = "A-secure-test-password-2026!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-tenant", new RegisterTenantRequest(
            company,
            shortName,
            company + " Admin",
            email,
            password,
            AppLanguage.English,
            "Africa/Johannesburg"), TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AuthenticatedSession>(response);
    }

    private static async Task<AuthenticatedSession> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password),
            TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AuthenticatedSession>(response);
    }

    private static void UseSession(HttpClient client, AuthenticatedSession session)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", session.Tenants.Single().TenantId.ToString());
    }

    private static void UseSession(HttpClient client, AuthenticatedSession session, Guid tenantId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync(TestCancellationToken));
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, TestCancellationToken))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
