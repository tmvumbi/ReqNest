using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Infrastructure.Integrations;

public sealed class WebhookEventPublisher(ReqNestDbContext dbContext) : IWebhookEventPublisher
{
    public async Task PublishAsync(
        Guid tenantId,
        string eventType,
        string eventKey,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await dbContext.WebhookSubscriptions
            .Where(entity => entity.IsActive && entity.EventTypes.Contains(eventType))
            .Select(entity => entity.Id)
            .ToArrayAsync(cancellationToken);
        var payloadJson = JsonSerializer.Serialize(new
        {
            id = eventKey,
            type = eventType,
            occurredAt = DateTimeOffset.UtcNow,
            data = payload,
        });
        foreach (var subscriptionId in subscriptions)
        {
            dbContext.WebhookDeliveries.Add(new WebhookDelivery
            {
                TenantId = tenantId,
                SubscriptionId = subscriptionId,
                EventType = eventType,
                EventKey = eventKey,
                PayloadJson = payloadJson,
            });
        }
    }
}
