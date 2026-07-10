using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Notifications;
using ReqNest.Core.Reports;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Tickets;
using ReqNest.Core.Workflows;
using ReqNest.Core.Views;

namespace ReqNest.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(1024).IsRequired();
        builder.HasIndex(entity => entity.NormalizedEmail).IsUnique();
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.UserAgent).HasMaxLength(512);
        builder.HasIndex(entity => entity.TokenHash).IsUnique();
        builder.HasIndex(entity => new { entity.UserId, entity.ExpiresAt });
    }
}

public sealed class AccountTokenConfiguration : IEntityTypeConfiguration<AccountToken>
{
    public void Configure(EntityTypeBuilder<AccountToken> builder)
    {
        builder.Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(entity => entity.TokenHash).IsUnique();
        builder.HasIndex(entity => new { entity.UserId, entity.Purpose, entity.ExpiresAt });
    }
}

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ShortName).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.PrimaryColor).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.LogoBlobName).HasMaxLength(512);
        builder.Property(entity => entity.LogoContentType).HasMaxLength(100);
        builder.Property(entity => entity.DarkLogoBlobName).HasMaxLength(512);
        builder.Property(entity => entity.DarkLogoContentType).HasMaxLength(100);
        builder.Property(entity => entity.SupportContact).HasMaxLength(320);
        builder.Property(entity => entity.ReportFooterText).HasMaxLength(500);
    }
}

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId }).IsUnique();
        builder.Property(entity => entity.InvitationTokenHash).HasMaxLength(128);
        builder.HasOne(entity => entity.Tenant)
            .WithMany(entity => entity.Memberships)
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.User)
            .WithMany(entity => entity.Memberships)
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoleGrantConfiguration : IEntityTypeConfiguration<RoleGrant>
{
    public void Configure(EntityTypeBuilder<RoleGrant> builder)
    {
        builder.HasIndex(entity => new
        {
            entity.TenantMembershipId,
            entity.Role,
            entity.AllProjects,
        });
        builder.HasOne(entity => entity.TenantMembership)
            .WithMany(entity => entity.RoleGrants)
            .HasForeignKey(entity => entity.TenantMembershipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoleGrantProjectConfiguration : IEntityTypeConfiguration<RoleGrantProject>
{
    public void Configure(EntityTypeBuilder<RoleGrantProject> builder)
    {
        builder.HasKey(entity => new { entity.RoleGrantId, entity.ProjectId });
        builder.HasOne(entity => entity.RoleGrant)
            .WithMany(entity => entity.ProjectScopes)
            .HasForeignKey(entity => entity.RoleGrantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Project)
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.Property(entity => entity.Key).HasMaxLength(12).IsRequired();
        builder.Property(entity => entity.NameEnglish).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NameFrench).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(2000);
        builder.HasIndex(entity => new { entity.TenantId, entity.Key }).IsUnique();
        builder.HasOne(entity => entity.Tenant)
            .WithMany(entity => entity.Projects)
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Workflow)
            .WithMany()
            .HasForeignKey(entity => entity.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).IsUnique();
        builder.HasOne(entity => entity.Tenant)
            .WithMany(entity => entity.Workflows)
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Project)
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.Property(entity => entity.Key).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.LabelEnglish).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.LabelFrench).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Color).HasMaxLength(20).IsRequired();
        builder.HasIndex(entity => new { entity.WorkflowId, entity.Key }).IsUnique();
        builder.HasIndex(entity => new { entity.WorkflowId, entity.Order }).IsUnique();
    }
}

public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.Property(entity => entity.NameEnglish).HasMaxLength(120);
        builder.Property(entity => entity.NameFrench).HasMaxLength(120);
        builder.HasIndex(entity => new
        {
            entity.WorkflowId,
            entity.FromStatusId,
            entity.ToStatusId,
        }).IsUnique();
        builder.HasOne(entity => entity.FromStatus)
            .WithMany()
            .HasForeignKey(entity => entity.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ToStatus)
            .WithMany()
            .HasForeignKey(entity => entity.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(entity => entity.Key).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.CreationKey).HasMaxLength(120);
        builder.Property(entity => entity.Title).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(100_000).IsRequired();
        builder.Property(entity => entity.DescriptionPlainText).HasMaxLength(100_000).IsRequired();
        builder.Property(entity => entity.ResolutionSummary).HasMaxLength(10_000);
        builder.Property(entity => entity.Labels).HasColumnType("text[]");
        builder.Property(entity => entity.Version).IsRowVersion();
        builder.HasIndex(entity => new { entity.TenantId, entity.Key }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.CreationKey }).IsUnique();
        builder.HasIndex(entity => new { entity.ProjectId, entity.Number }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.ProjectId, entity.WorkflowStatusId });
        builder.HasIndex(entity => new { entity.TenantId, entity.AssigneeUserId, entity.IsArchived });
        builder.HasOne(entity => entity.Project)
            .WithMany()
            .HasForeignKey(entity => entity.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.WorkflowStatus)
            .WithMany()
            .HasForeignKey(entity => entity.WorkflowStatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ReporterUser)
            .WithMany()
            .HasForeignKey(entity => entity.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.AssigneeUser)
            .WithMany()
            .HasForeignKey(entity => entity.AssigneeUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.Property(entity => entity.Body).HasMaxLength(50_000).IsRequired();
        builder.Property(entity => entity.BodyPlainText).HasMaxLength(50_000).IsRequired();
        builder.HasIndex(entity => new { entity.TicketId, entity.CreatedAt });
        builder.HasOne(entity => entity.AuthorUser)
            .WithMany()
            .HasForeignKey(entity => entity.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TicketCommentRevisionConfiguration : IEntityTypeConfiguration<TicketCommentRevision>
{
    public void Configure(EntityTypeBuilder<TicketCommentRevision> builder)
    {
        builder.Property(entity => entity.PreviousBody).HasMaxLength(50_000).IsRequired();
        builder.HasOne(entity => entity.TicketComment)
            .WithMany(entity => entity.Revisions)
            .HasForeignKey(entity => entity.TicketCommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TicketWatcherConfiguration : IEntityTypeConfiguration<TicketWatcher>
{
    public void Configure(EntityTypeBuilder<TicketWatcher> builder)
    {
        builder.HasKey(entity => new { entity.TicketId, entity.UserId });
        builder.HasOne(entity => entity.Ticket)
            .WithMany(entity => entity.Watchers)
            .HasForeignKey(entity => entity.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        builder.Property(entity => entity.Comment).HasMaxLength(5000);
        builder.HasIndex(entity => new { entity.TicketId, entity.CreatedAt });
    }
}

public sealed class TicketRelationshipConfiguration : IEntityTypeConfiguration<TicketRelationship>
{
    public void Configure(EntityTypeBuilder<TicketRelationship> builder)
    {
        builder.HasIndex(entity => new
        {
            entity.SourceTicketId,
            entity.TargetTicketId,
            entity.Type,
        }).IsUnique();
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.Property(entity => entity.ContainerName).HasMaxLength(63).IsRequired();
        builder.Property(entity => entity.BlobName).HasMaxLength(1024).IsRequired();
        builder.Property(entity => entity.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(entity => entity.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ChecksumSha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.BlobName }).IsUnique();
        builder.HasOne(entity => entity.TicketComment)
            .WithMany(entity => entity.Attachments)
            .HasForeignKey(entity => entity.TicketCommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(entity => entity.EventKey).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.SummaryEnglish).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.SummaryFrench).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.DeepLink).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.GroupKey).HasMaxLength(160);
        builder.HasIndex(entity => new { entity.RecipientUserId, entity.EventKey }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.RecipientUserId, entity.ReadAt });
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder) =>
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId }).IsUnique();
}

public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.Property(entity => entity.Name).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.FiltersJson).HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.SortJson).HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.ColumnsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.GroupBy).HasMaxLength(40);
        builder.HasIndex(entity => new { entity.TenantId, entity.OwnerUserId, entity.Name }).IsUnique();
    }
}

public sealed class ReportExportConfiguration : IEntityTypeConfiguration<ReportExport>
{
    public void Configure(EntityTypeBuilder<ReportExport> builder)
    {
        builder.Property(entity => entity.ReportType).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.FilterSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ContainerName).HasMaxLength(63);
        builder.Property(entity => entity.BlobName).HasMaxLength(1024);
        builder.Property(entity => entity.FailureReason).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.TenantId, entity.RequestedByUserId, entity.CreatedAt });
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.Property(entity => entity.Action).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.TargetType).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.TargetId).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.CorrelationId).HasMaxLength(120);
        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedAt });
    }
}
