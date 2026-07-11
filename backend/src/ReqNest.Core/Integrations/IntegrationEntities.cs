using ReqNest.Core.Common;
using ReqNest.Core.Identity;

namespace ReqNest.Core.Integrations;

public sealed class RequesterIdentity : Entity
{
    public Guid TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public AppLanguage PreferredLanguage { get; set; }
}

public sealed class RequesterTicketAccess : Entity
{
    public Guid TenantId { get; set; }

    public Guid RequesterIdentityId { get; set; }

    public Guid TicketId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastAccessedAt { get; set; }
}

public sealed class RequesterComment : Entity
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Guid RequesterIdentityId { get; set; }

    public string Body { get; set; } = string.Empty;

    public string BodyPlainText { get; set; } = string.Empty;

    public bool IsHidden { get; set; }
}

public sealed class InboundEmailChannel : Entity
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    public string DefaultTypeKey { get; set; } = "ServiceRequest";

    public string DefaultPriorityKey { get; set; } = "Normal";

    public bool IsActive { get; set; } = true;
}

public enum InboundEmailStatus
{
    Received,
    Processed,
    Rejected,
}

public sealed class InboundEmailMessage : Entity
{
    public Guid TenantId { get; set; }

    public Guid ChannelId { get; set; }

    public Guid? TicketId { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public string? InReplyTo { get; set; }

    public string SenderEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public InboundEmailStatus Status { get; set; }

    public string? FailureCode { get; set; }
}

public sealed class ApiToken : Entity
{
    public Guid TenantId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public string[] Scopes { get; set; } = [];

    public Guid[] ProjectIds { get; set; } = [];

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed,
}

public sealed class WebhookSubscription : Entity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ProtectedSecret { get; set; } = string.Empty;

    public string[] EventTypes { get; set; } = [];

    public bool IsActive { get; set; } = true;
}

public sealed class WebhookDelivery : Entity
{
    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EventKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public WebhookDeliveryStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DeliveredAt { get; set; }

    public int? LastStatusCode { get; set; }

    public string? LastError { get; set; }

}

public sealed class TenantSsoConfiguration : Entity
{
    public Guid TenantId { get; set; }

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ProtectedClientSecret { get; set; } = string.Empty;

    public string[] AllowedEmailDomains { get; set; } = [];

    public bool IsEnabled { get; set; }

    public bool RequireSso { get; set; }
}

public sealed class ExternalIdentityLink : Entity
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Provider { get; set; } = "oidc";

    public string Subject { get; set; } = string.Empty;

    public string EmailSnapshot { get; set; } = string.Empty;
}

public sealed class SsoAuthenticationExchange : Entity
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

public enum KnowledgeArticleStatus
{
    Draft,
    Published,
    Archived,
}

public enum KnowledgeArticleVisibility
{
    Internal,
    Requesters,
}

public sealed class KnowledgeArticle : Entity
{
    public Guid TenantId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;


    public string Body { get; set; } = string.Empty;


    public string SearchText { get; set; } = string.Empty;

    public KnowledgeArticleStatus Status { get; set; }

    public KnowledgeArticleVisibility Visibility { get; set; }

    public Guid AuthorUserId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class TicketKnowledgeArticle
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Guid KnowledgeArticleId { get; set; }

    public Guid LinkedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum IntegrationConnectionStatus
{
    Disabled,
    Connected,
    Error,
}

public sealed class IntegrationConnection : Entity
{
    public Guid TenantId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ProtectedConfiguration { get; set; } = string.Empty;

    public IntegrationConnectionStatus Status { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public string? LastError { get; set; }

    public int RetryAttempts { get; set; }

    public DateTimeOffset? NextRetryAt { get; set; }
}

public enum AiAssistanceKind
{
    Summarize,
    SuggestReply,
    Classify,
}

public enum AiAssistanceStatus
{
    Draft,
    Accepted,
    Rejected,
    Failed,
}

public sealed class AiTenantConfiguration : Entity
{
    public Guid TenantId { get; set; }

    public bool IsEnabled { get; set; }

    public string Provider { get; set; } = "None";

    public string? ProtectedCredential { get; set; }

    public AiAssistanceKind[] AllowedKinds { get; set; } = [];

    public bool RequireHumanReview { get; set; } = true;

    public bool AllowAttachmentContent { get; set; }

    public DateTimeOffset? NonTrainingAssuranceAcceptedAt { get; set; }

    public string EvaluationVersion { get; set; } = "safe-draft-v1";
}

public sealed class AiAssistanceRequest : Entity
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public AiAssistanceKind Kind { get; set; }

    public string InputFingerprint { get; set; } = string.Empty;

    public string DraftOutput { get; set; } = string.Empty;

    public AiAssistanceStatus Status { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? FailureCode { get; set; }

    public decimal EvaluationScore { get; set; }
}
