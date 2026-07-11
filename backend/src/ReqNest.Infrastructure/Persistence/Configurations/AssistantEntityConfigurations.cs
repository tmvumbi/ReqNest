using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReqNest.Core.Assistant;
using ReqNest.Core.Identity;

namespace ReqNest.Infrastructure.Persistence.Configurations;

public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.UserId, entity.LastMessageAt });
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.Property(entity => entity.Role).HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Content).IsRequired();
        builder.Property(entity => entity.ToolCallsJson).HasColumnType("jsonb");
        builder.Property(entity => entity.ToolCallId).HasMaxLength(120);
        builder.Property(entity => entity.ToolName).HasMaxLength(120);
        builder.HasIndex(entity => new { entity.ConversationId, entity.CreatedAt });
        builder.HasOne(entity => entity.Conversation)
            .WithMany(entity => entity.Messages)
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
