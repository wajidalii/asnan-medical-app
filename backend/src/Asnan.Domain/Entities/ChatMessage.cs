using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// A single message in a <see cref="ChatConversation"/> — ARCHITECTURE.md
/// §9 (issue #28). Persisted before broadcast ("durable-first") — history
/// is always consistent with what was actually delivered, never the reverse.
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid ChatConversationId { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;

    public Guid SenderUserId { get; set; }

    public User SenderUser { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime SentAtUtc { get; set; }
}
