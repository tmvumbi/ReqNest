using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Tickets;

namespace ReqNest.Infrastructure.Persistence.Configurations;

public sealed class RequesterIdentityConfiguration : IEntityTypeConfiguration<RequesterIdentity>
{
    public void Configure(EntityTypeBuilder<RequesterIdentity> builder)
    {
        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(160).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.NormalizedEmail }).IsUnique();
    }
}

public sealed class RequesterTicketAccessConfiguration : IEntityTypeConfiguration<RequesterTicketAccess>
{
    public void Configure(EntityTypeBuilder<RequesterTicketAccess> builder)
    {
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(entity => entity.TokenHash).IsUnique();
        builder.HasIndex(entity => new { entity.RequesterIdentityId, entity.TicketId }).IsUnique();
        builder.HasOne<RequesterIdentity>().WithMany().HasForeignKey(entity => entity.RequesterIdentityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(entity => entity.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InboundEmailChannelConfiguration : IEntityTypeConfiguration<InboundEmailChannel>
{
    public void Configure(EntityTypeBuilder<InboundEmailChannel> builder)
    {
        builder.Property(entity => entity.Address).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.SecretHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.DefaultTypeKey).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.DefaultPriorityKey).HasMaxLength(80).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.Address }).IsUnique();
        builder.HasOne<Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RequesterCommentConfiguration : IEntityTypeConfiguration<RequesterComment>
{
    public void Configure(EntityTypeBuilder<RequesterComment> builder)
    {
        builder.Property(entity => entity.Body).HasMaxLength(50_000).IsRequired();
        builder.Property(entity => entity.BodyPlainText).HasMaxLength(20_000).IsRequired();
        builder.HasIndex(entity => new { entity.TicketId, entity.CreatedAt });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(entity => entity.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RequesterIdentity>().WithMany().HasForeignKey(entity => entity.RequesterIdentityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InboundEmailMessageConfiguration : IEntityTypeConfiguration<InboundEmailMessage>
{
    public void Configure(EntityTypeBuilder<InboundEmailMessage> builder)
    {
        builder.Property(entity => entity.MessageId).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.InReplyTo).HasMaxLength(500);
        builder.Property(entity => entity.SenderEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.Subject).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.FailureCode).HasMaxLength(160);
        builder.HasIndex(entity => new { entity.ChannelId, entity.MessageId }).IsUnique();
        builder.HasOne<InboundEmailChannel>().WithMany().HasForeignKey(entity => entity.ChannelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(entity => entity.TicketId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Prefix).HasMaxLength(24).IsRequired();
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Scopes).HasColumnType("text[]");
        builder.Property(entity => entity.ProjectIds).HasColumnType("uuid[]");
        builder.HasIndex(entity => entity.TokenHash).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Prefix });
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Url).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.ProtectedSecret).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.EventTypes).HasColumnType("text[]");
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).IsUnique();
    }
}

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.Property(entity => entity.EventType).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.EventKey).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.LastError).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.SubscriptionId, entity.EventKey }).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.NextAttemptAt });
        builder.HasOne<WebhookSubscription>().WithMany().HasForeignKey(entity => entity.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TenantSsoConfigurationConfiguration : IEntityTypeConfiguration<TenantSsoConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantSsoConfiguration> builder)
    {
        builder.Property(entity => entity.Authority).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.ClientId).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.ProtectedClientSecret).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.AllowedEmailDomains).HasColumnType("text[]");
        builder.HasIndex(entity => entity.TenantId).IsUnique();
    }
}

public sealed class ExternalIdentityLinkConfiguration : IEntityTypeConfiguration<ExternalIdentityLink>
{
    public void Configure(EntityTypeBuilder<ExternalIdentityLink> builder)
    {
        builder.Property(entity => entity.Provider).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Subject).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.EmailSnapshot).HasMaxLength(320).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.Provider, entity.Subject }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.Provider }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SsoAuthenticationExchangeConfiguration : IEntityTypeConfiguration<SsoAuthenticationExchange>
{
    public void Configure(EntityTypeBuilder<SsoAuthenticationExchange> builder)
    {
        builder.Property(entity => entity.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => entity.CodeHash).IsUnique();
        builder.HasIndex(entity => entity.ExpiresAt);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.Property(entity => entity.Slug).HasMaxLength(180).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Body).HasMaxLength(100_000).IsRequired();
        builder.Property(entity => entity.SearchText).HasMaxLength(20_000).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.Slug }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.Visibility });
        builder.HasOne<Project>().WithMany().HasForeignKey(entity => entity.ProjectId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TicketKnowledgeArticleConfiguration : IEntityTypeConfiguration<TicketKnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<TicketKnowledgeArticle> builder)
    {
        builder.HasKey(entity => new { entity.TicketId, entity.KnowledgeArticleId });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(entity => entity.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(entity => entity.KnowledgeArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.LinkedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.Property(entity => entity.Provider).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.ProtectedConfiguration).HasMaxLength(20_000).IsRequired();
        builder.Property(entity => entity.LastError).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.TenantId, entity.Provider, entity.Name }).IsUnique();
    }
}

public sealed class AiTenantConfigurationConfiguration : IEntityTypeConfiguration<AiTenantConfiguration>
{
    public void Configure(EntityTypeBuilder<AiTenantConfiguration> builder)
    {
        builder.Property(entity => entity.Provider).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.ProtectedCredential).HasMaxLength(8000);
        builder.Property(entity => entity.AllowedKinds).HasColumnType("integer[]");
        builder.HasIndex(entity => entity.TenantId).IsUnique();
    }
}

public sealed class AiAssistanceRequestConfiguration : IEntityTypeConfiguration<AiAssistanceRequest>
{
    public void Configure(EntityTypeBuilder<AiAssistanceRequest> builder)
    {
        builder.Property(entity => entity.InputFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.DraftOutput).HasMaxLength(50_000).IsRequired();
        builder.Property(entity => entity.FailureCode).HasMaxLength(160);
        builder.HasIndex(entity => new { entity.TenantId, entity.TicketId, entity.CreatedAt });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(entity => entity.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
