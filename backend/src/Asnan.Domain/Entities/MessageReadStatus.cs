using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Per-participant last-read tracking, one row per (conversation, user) —
/// ARCHITECTURE.md §9 (issue #28). Powers unread counts and read receipts;
/// absence of a row for a given (conversation, user) means "never read anything".
/// </summary>
public class MessageReadStatus : BaseEntity
{
    public Guid ChatConversationId { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid LastReadMessageId { get; set; }

    public DateTime LastReadAtUtc { get; set; }
}
