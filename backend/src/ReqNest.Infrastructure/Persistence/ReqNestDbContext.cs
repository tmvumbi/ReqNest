using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Assistant;
using ReqNest.Core.Auditing;
using ReqNest.Core.Common;
using ReqNest.Core.Configuration;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Core.Notifications;
using ReqNest.Core.Reports;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Tickets;
using ReqNest.Core.Workflows;
using ReqNest.Core.Views;

namespace ReqNest.Infrastructure.Persistence;

public sealed class ReqNestDbContext(
    DbContextOptions<ReqNestDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();

    public DbSet<CustomRole> CustomRoles => Set<CustomRole>();

    public DbSet<CustomRoleGrant> CustomRoleGrants => Set<CustomRoleGrant>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();

    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    public DbSet<TicketWatcher> TicketWatchers => Set<TicketWatcher>();

    public DbSet<TicketCommentRevision> TicketCommentRevisions => Set<TicketCommentRevision>();

    public DbSet<TicketStatusHistory> TicketStatusHistory => Set<TicketStatusHistory>();

    public DbSet<TicketRelationship> TicketRelationships => Set<TicketRelationship>();

    public DbSet<TicketTypeDefinition> TicketTypeDefinitions => Set<TicketTypeDefinition>();

    public DbSet<TicketPriorityDefinition> TicketPriorityDefinitions => Set<TicketPriorityDefinition>();

    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();

    public DbSet<TicketCustomFieldValue> TicketCustomFieldValues => Set<TicketCustomFieldValue>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<SlaPriorityTarget> SlaPriorityTargets => Set<SlaPriorityTarget>();

    public DbSet<SlaHoliday> SlaHolidays => Set<SlaHoliday>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    public DbSet<SavedView> SavedViews => Set<SavedView>();

    public DbSet<ReportExport> ReportExports => Set<ReportExport>();

    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<RequesterIdentity> RequesterIdentities => Set<RequesterIdentity>();

    public DbSet<RequesterTicketAccess> RequesterTicketAccesses => Set<RequesterTicketAccess>();

    public DbSet<RequesterComment> RequesterComments => Set<RequesterComment>();

    public DbSet<InboundEmailChannel> InboundEmailChannels => Set<InboundEmailChannel>();

    public DbSet<InboundEmailMessage> InboundEmailMessages => Set<InboundEmailMessage>();

    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<TenantSsoConfiguration> TenantSsoConfigurations => Set<TenantSsoConfiguration>();

    public DbSet<ExternalIdentityLink> ExternalIdentityLinks => Set<ExternalIdentityLink>();

    public DbSet<SsoAuthenticationExchange> SsoAuthenticationExchanges => Set<SsoAuthenticationExchange>();

    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();

    public DbSet<TicketKnowledgeArticle> TicketKnowledgeArticles => Set<TicketKnowledgeArticle>();

    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();

    public DbSet<AiTenantConfiguration> AiTenantConfigurations => Set<AiTenantConfiguration>();

    public DbSet<AiAssistanceRequest> AiAssistanceRequests => Set<AiAssistanceRequest>();

    public DbSet<AiConversation> AiConversations => Set<AiConversation>();

    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReqNestDbContext).Assembly);

        modelBuilder.Entity<Tenant>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.Id == tenantContext.TenantId);
        modelBuilder.Entity<TenantMembership>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RoleGrant>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RoleGrantProject>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomRole>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomRoleGrant>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomRoleGrantProject>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Project>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Workflow>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkflowStatus>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WorkflowTransition>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Ticket>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketComment>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketCommentRevision>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketWatcher>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketStatusHistory>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketRelationship>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketTypeDefinition>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketPriorityDefinition>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<CustomFieldDefinition>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketCustomFieldValue>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SlaPolicy>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SlaPriorityTarget>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SlaHoliday>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Attachment>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<NotificationPreference>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<EmailOutboxMessage>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SavedView>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ReportExport>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ReportSchedule>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AuditEvent>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RequesterIdentity>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RequesterTicketAccess>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<RequesterComment>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<InboundEmailChannel>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<InboundEmailMessage>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ApiToken>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WebhookSubscription>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<WebhookDelivery>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TenantSsoConfiguration>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<ExternalIdentityLink>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<SsoAuthenticationExchange>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<KnowledgeArticle>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<TicketKnowledgeArticle>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<IntegrationConnection>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AiTenantConfiguration>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AiAssistanceRequest>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AiConversation>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
        modelBuilder.Entity<AiChatMessage>().HasQueryFilter(entity =>
            tenantContext.TenantId != null && entity.TenantId == tenantContext.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
