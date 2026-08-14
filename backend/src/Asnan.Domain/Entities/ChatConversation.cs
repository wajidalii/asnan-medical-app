using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// One conversation per <see cref="Appointment"/> — ARCHITECTURE.md §9.
/// Created automatically the instant an appointment becomes Scheduled (see
/// PaymentService); this is the enforcement point for "no chat before a
/// scheduled appointment" — message/pagination/read-state (Milestone 7)
/// build on top of this.
/// </summary>
public class ChatConversation : BaseEntity
{
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
}
