using ReqNest.Core.Notifications;

namespace ReqNest.Infrastructure.Notifications;

public sealed class DevelopmentEmailDeliveryProvider : IEmailDeliveryProvider
{
    public Task DeliverAsync(OutboundEmail message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
