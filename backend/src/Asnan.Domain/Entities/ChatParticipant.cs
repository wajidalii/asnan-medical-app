using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Membership row authorizing exactly one user into a <see cref="ChatConversation"/>
/// — ARCHITECTURE.md §9. Always exactly two per conversation (the appointment's
/// patient and doctor); authorization checks in Milestone 7 are against these
/// rows, not a general role check.
/// </summary>
public class ChatParticipant : BaseEntity
{
    public Guid ChatConversationId { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
