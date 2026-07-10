using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReqNest.Core.Identity;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Tests.Api;

[Collection(ApiCollection.Name)]
public sealed class AuthenticationEndpointsTests(ReqNestApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registration_creates_a_hashed_administrator_and_default_workflow()
    {
        var client = factory.CreateClient();
        var request = NewRegistration("Acme Support", "ACME", "admin@acme.test");

        var response = await client.PostAsJsonAsync("/api/auth/register-tenant", request, TestCancellationToken);

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<AuthenticatedSession>(JsonOptions, TestCancellationToken);
        Assert.NotNull(session);
        Assert.NotEmpty(session.AccessToken);
        Assert.Single(session.Tenants);
        Assert.Contains(AppRole.TenantAdministrator, session.Tenants.Single().Roles);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleAsync(entity => entity.Email == request.Email, TestCancellationToken);
        Assert.NotEqual(request.Password, user.PasswordHash);
        Assert.DoesNotContain(request.Password, user.PasswordHash, StringComparison.Ordinal);

        var tenantId = session.Tenants.Single().TenantId;
        var workflow = await dbContext.Workflows
            .IgnoreQueryFilters()
            .Include(entity => entity.Statuses)
            .Include(entity => entity.Transitions)
            .SingleAsync(entity => entity.TenantId == tenantId && entity.IsDefault, TestCancellationToken);
        Assert.Equal(3, workflow.Statuses.Count);
        Assert.Equal(new[] { "TODO", "IN_PROGRESS", "DONE" },
            workflow.Statuses.OrderBy(status => status.Order).Select(status => status.Key));
        Assert.Equal(4, workflow.Transitions.Count);
    }

    [Fact]
    public async Task Login_logout_and_tenant_boundary_are_enforced()
    {
        var client = factory.CreateClient();
        var alpha = await RegisterAsync(client, NewRegistration("Alpha", "ALPHA", "admin@alpha.test"));
        var beta = await RegisterAsync(client, NewRegistration("Beta", "BETA", "admin@beta.test"));

        var unauthenticated = await client.GetAsync("/api/auth/me", TestCancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alpha.AccessToken);
        var currentUser = await client.GetAsync("/api/auth/me", TestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, currentUser.StatusCode);

        client.DefaultRequestHeaders.Add("X-Tenant-Id", beta.Tenants.Single().TenantId.ToString());
        var crossTenant = await client.GetAsync("/api/auth/me", TestCancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenant.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var logout = await client.PostAsync("/api/auth/logout", null, TestCancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.GetAsync("/api/auth/me", TestCancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Invalid_credentials_use_a_generic_response()
    {
        var client = factory.CreateClient();
        var request = NewRegistration("Login Corp", "LOGIN", "admin@login.test");
        await RegisterAsync(client, request);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new
        {
            request.Email,
            Password = "definitely-not-the-password",
        }, TestCancellationToken);
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "unknown@example.test",
            Password = "definitely-not-the-password",
        }, TestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
    }

    [Fact]
    public async Task Authentication_endpoints_are_rate_limited_by_client_address()
    {
        using var rateLimitedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:RateLimit:PermitLimit"] = "2",
                    ["Authentication:RateLimit:WindowSeconds"] = "60",
                })));
        var client = rateLimitedFactory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "rate-limit@example.test",
                Password = "not-the-password",
            }, TestCancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "rate-limit@example.test",
            Password = "not-the-password",
        }, TestCancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: TestCancellationToken);
        Assert.Equal("rate_limited", problem.GetProperty("code").GetString());
    }

    private static RegistrationRequest NewRegistration(string companyName, string shortName, string email) =>
        new(
            companyName,
            shortName,
            $"{companyName} Admin",
            email,
            "A-secure-test-password-2026!",
            "English",
            "Africa/Johannesburg");

    private static async Task<AuthenticatedSession> RegisterAsync(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-tenant", request, TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthenticatedSession>(JsonOptions, TestCancellationToken))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record RegistrationRequest(
        string CompanyName,
        string CompanyShortName,
        string DisplayName,
        string Email,
        string Password,
        string Language,
        string TimeZone);
}
