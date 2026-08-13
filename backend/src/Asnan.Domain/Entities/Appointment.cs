using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// A confirmed-or-in-progress booking — created at checkout time in
/// <see cref="AppointmentStatus.PaymentPending"/> (see the payments-checkout
/// issue), not at hold time (ARCHITECTURE.md §7). All status changes go
/// through <see cref="AppointmentStateMachine"/>; never set <see cref="Status"/>
/// directly outside it.
/// </summary>
public class Appointment : BaseEntity
{
    public Guid DoctorProfileId { get; set; }

    public DoctorProfile DoctorProfile { get; set; } = null!;

    public Guid PatientUserId { get; set; }

    public User PatientUser { get; set; } = null!;

    public DateTime SlotStartUtc { get; set; }

    public DateTime SlotEndUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.PaymentPending;

    /// <summary>Fee snapshot at booking time — the doctor's current fee can change later without altering this appointment's price.</summary>
    public decimal ConsultationFee { get; set; }

    /// <summary>ISO 4217 currency code, snapshot at booking time.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// The <see cref="AppointmentHold"/> this appointment was created from —
    /// informational lineage only (the hold row itself, not this reference,
    /// is what enforces slot-uniqueness), so intentionally not a mapped FK.
    /// </summary>
    public Guid SourceHoldId { get; set; }

    public ICollection<AppointmentStatusHistory> StatusHistory { get; set; } = new List<AppointmentStatusHistory>();
}
