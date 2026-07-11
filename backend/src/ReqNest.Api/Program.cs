using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReqNest.Api.Authentication;
using ReqNest.Api.Background;
using ReqNest.Api.Endpoints;
using ReqNest.Infrastructure;
using ReqNest.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Local-only secrets (AI keys, etc.) live outside the committed appsettings files.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ReqNest.Api.Assistant.OpenRouterClient>();
builder.Services.AddSingleton<ReqNest.Api.Assistant.AssistantToolService>();
builder.Services.AddInfrastructure(builder.Configuration);
if (builder.Configuration.GetValue<bool>("Reports:RunScheduleWorker"))
{
    }
if (builder.Configuration.GetValue<bool>("Integrations:RunWebhookWorker"))
{
    builder.Services.AddHostedService<WebhookDeliveryWorker>();
}
if (builder.Configuration.GetValue<bool>("Integrations:RunConnectionWorker"))
{
    builder.Services.AddHostedService<IntegrationConnectionWorker>();
}
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services
    .AddAuthentication(SessionAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("authentication", context =>
    {
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var permitLimit = Math.Max(
            1,
            configuration.GetValue("Authentication:RateLimit:PermitLimit", 30));
        var windowSeconds = Math.Max(
            1,
            configuration.GetValue("Authentication:RateLimit:WindowSeconds", 60));
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
    options.OnRejected = async (rejectionContext, cancellationToken) =>
    {
        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many authentication requests.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "rate_limited",
                ["traceId"] = rejectionContext.HttpContext.TraceIdentifier,
            }).ExecuteAsync(rejectionContext.HttpContext);
    };
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ReqNestDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapProjectEndpoints();
app.MapWorkflowEndpoints();
app.MapTicketEndpoints();
app.MapCollaborationEndpoints();
app.MapAttachmentEndpoints();
app.MapMemberEndpoints();
app.MapNotificationEndpoints();
app.MapSavedViewEndpoints();
app.MapReportEndpoints();
app.MapAdministrationEndpoints();
app.MapConfigurationEndpoints();
app.MapCustomRoleEndpoints();
app.MapRelationshipEndpoints();
app.MapOperationsEndpoints();
app.MapRequesterPortalEndpoints();
app.MapApiTokenEndpoints();
app.MapKnowledgeEndpoints();
app.MapIntegrationAdministrationEndpoints();
app.MapInboundEmailEndpoints();
app.MapSsoAuthenticationEndpoints();
app.MapAiAssistanceEndpoints();
app.MapAssistantEndpoints();

app.Run();

public partial class Program;
