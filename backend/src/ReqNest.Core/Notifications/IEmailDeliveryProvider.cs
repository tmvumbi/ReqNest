namespace ReqNest.Core.Notifications;

public sealed record OutboundEmail(
    string RecipientEmail,
    string Subject,
    string BodyText,
    string BodyHtml);

public interface IEmailDeliveryProvider
{
    Task DeliverAsync(OutboundEmail message, CancellationToken cancellationToken = default);
}
