using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// Append-only audit trail of every <see cref="Appointment"/> status
/// transition (who, when, from, to, reason) — ARCHITECTURE.md §7. Written
/// exclusively by <see cref="AppointmentStateMachine"/>.
/// </summary>
public class AppointmentStatusHistory : BaseEntity
{
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    /// <summary>Null only for the row recording the appointment's initial creation.</summary>
    public AppointmentStatus? FromStatus { get; set; }

    public AppointmentStatus ToStatus { get; set; }

    /// <summary>Null for system-initiated transitions (e.g. payment webhook, expiry sweep).</summary>
    public Guid? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}
