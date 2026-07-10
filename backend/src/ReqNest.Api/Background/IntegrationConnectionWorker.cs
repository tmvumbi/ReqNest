using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Background;

public sealed class IntegrationConnectionWorker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory clients,
    IDataProtectionProvider protectionProvider,
    ILogger<IntegrationConnectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            do
            {
                try { await ProcessAsync(stoppingToken); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Integration connection processing failed.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        Guid[] ids;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
            ids = await db.IntegrationConnections.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.NextRetryAt != null && item.NextRetryAt <= DateTimeOffset.UtcNow)
                .OrderBy(item => item.NextRetryAt).Take(20).Select(item => item.Id).ToArrayAsync(cancellationToken);
        }
        foreach (var id in ids) await TestAsync(id, cancellationToken);
    }

    private async Task TestAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var entity = await db.IntegrationConnections.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity?.NextRetryAt is null) return;
        entity.LastCheckedAt = DateTimeOffset.UtcNow;
        entity.RetryAttempts++;
        try
        {
            var raw = protectionProvider.CreateProtector("ReqNest.Integrations.v1").Unprotect(entity.ProtectedConfiguration);
            using var configuration = JsonDocument.Parse(raw);
            if (!configuration.RootElement.TryGetProperty("healthCheckUrl", out var urlProperty) ||
                !Uri.TryCreate(urlProperty.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !await WebhookDeliveryWorker.IsPublicDestinationAsync(uri, cancellationToken))
                throw new InvalidOperationException("health_check_url_invalid");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (configuration.RootElement.TryGetProperty("bearerToken", out var bearer) && !string.IsNullOrWhiteSpace(bearer.GetString()))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer.GetString());
            using var response = await clients.CreateClient().SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("http_" + (int)response.StatusCode);
            entity.Status = IntegrationConnectionStatus.Connected;
            entity.LastError = null;
            entity.NextRetryAt = null;
            entity.RetryAttempts = 0;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or CryptographicException or JsonException or InvalidOperationException)
        {
            entity.Status = IntegrationConnectionStatus.Error;
            entity.LastError = exception.Message[..Math.Min(exception.Message.Length, 500)];
            entity.NextRetryAt = entity.RetryAttempts >= 6 ? null : DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, entity.RetryAttempts));
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
