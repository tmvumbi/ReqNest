using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Integrations;
using ReqNest.Core.Tenancy;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Background;

public sealed class WebhookDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider protectionProvider,
    ILogger<WebhookDeliveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Webhook delivery processing failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        Guid[] deliveryIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
            deliveryIds = await db.WebhookDeliveries.IgnoreQueryFilters().AsNoTracking()
                .Where(entity => entity.Status == WebhookDeliveryStatus.Pending && entity.NextAttemptAt <= DateTimeOffset.UtcNow)
                .OrderBy(entity => entity.NextAttemptAt).Take(50).Select(entity => entity.Id)
                .ToArrayAsync(cancellationToken);
        }

        foreach (var deliveryId in deliveryIds)
        {
            await DeliverAsync(deliveryId, cancellationToken);
        }
    }

    private async Task DeliverAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        var delivery = await db.WebhookDeliveries.IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == deliveryId, cancellationToken);
        if (delivery is null || delivery.Status != WebhookDeliveryStatus.Pending) return;
        var subscription = await db.WebhookSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == delivery.SubscriptionId && entity.IsActive, cancellationToken);
        if (subscription is null)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.LastError = "subscription_inactive";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = delivery.TenantId;
        try
        {
            var uri = new Uri(subscription.Url);
            if (!await IsPublicDestinationAsync(uri, cancellationToken))
            {
                throw new InvalidOperationException("destination_not_public");
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var secret = protectionProvider.CreateProtector("ReqNest.Webhooks.v1").Unprotect(subscription.ProtectedSecret);
            var signature = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(timestamp + "." + delivery.PayloadJson))).ToLowerInvariant();
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-ReqNest-Event", delivery.EventType);
            request.Headers.Add("X-ReqNest-Delivery", delivery.Id.ToString());
            request.Headers.Add("X-ReqNest-Timestamp", timestamp);
            request.Headers.Add("X-ReqNest-Signature", "sha256=" + signature);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ReqNest", "1.0"));
            using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
            delivery.Attempts++;
            delivery.LastStatusCode = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTimeOffset.UtcNow;
                delivery.LastError = null;
            }
            else
            {
                ScheduleRetry(delivery, "http_" + (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or CryptographicException or InvalidOperationException)
        {
            delivery.Attempts++;
            ScheduleRetry(delivery, exception.Message[..Math.Min(500, exception.Message.Length)]);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ScheduleRetry(WebhookDelivery delivery, string error)
    {
        delivery.LastError = error;
        if (delivery.Attempts >= 6)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
        }
        else
        {
            delivery.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, delivery.Attempts));
        }
    }

    internal static async Task<bool> IsPublicDestinationAsync(Uri uri, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        return addresses.Length > 0 && addresses.All(address =>
            !IPAddress.IsLoopback(address) &&
            !(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
              (address.GetAddressBytes()[0] == 10 || address.GetAddressBytes()[0] == 127 ||
               address.GetAddressBytes()[0] == 192 && address.GetAddressBytes()[1] == 168 ||
               address.GetAddressBytes()[0] == 172 && address.GetAddressBytes()[1] is >= 16 and <= 31)) &&
            !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal);
    }
}
