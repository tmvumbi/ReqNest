using ReqNest.Core.Common;

namespace ReqNest.Core.Assistant;

public sealed class AiConversation : Entity
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AiChatMessage> Messages { get; set; } = [];
}

public sealed class AiChatMessage : Entity
{
    public Guid TenantId { get; set; }

    public Guid ConversationId { get; set; }

    public AiConversation Conversation { get; set; } = null!;

    // user | assistant | tool
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    // Serialized model tool calls for assistant messages that requested tools.
    public string? ToolCallsJson { get; set; }

    // Set on tool result messages so history can be replayed to the model.
    public string? ToolCallId { get; set; }

    public string? ToolName { get; set; }

    public bool IsVoice { get; set; }
}
