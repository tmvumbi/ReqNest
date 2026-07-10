namespace ReqNest.Core.Integrations;

public interface IWebhookEventPublisher
{
    Task PublishAsync(
        Guid tenantId,
        string eventType,
        string eventKey,
        object payload,
        CancellationToken cancellationToken = default);
}
