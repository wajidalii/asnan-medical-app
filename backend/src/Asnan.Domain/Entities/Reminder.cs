using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// One (appointment, offset) reminder — issue #25. Created lazily by the
/// scanning background service only once it becomes due, and only for
/// appointments still <see cref="AppointmentStatus.Scheduled"/> at scan
/// time — a cancelled appointment is naturally excluded from ever getting
/// one created for it going forward (see ReminderSchedulingService's doc
/// comment for why that's sufficient "suppression on cancellation" without
/// needing to retroactively edit already-existing rows). The unique index
/// on (AppointmentId, OffsetMinutes) — see ReminderConfiguration — is what
/// actually guarantees a reminder is never sent twice, not application logic.
/// </summary>
public class Reminder : BaseEntity
{
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    /// <summary>Minutes before the appointment's SlotStartUtc this reminder fires — one of the configured ReminderOptions.OffsetsMinutes values.</summary>
    public int OffsetMinutes { get; set; }

    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;

    public DateTime? SentAtUtc { get; set; }
}
