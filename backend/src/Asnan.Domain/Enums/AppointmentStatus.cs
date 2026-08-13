namespace Asnan.Domain.Enums;

/// <summary>
/// Appointment-entity states — ARCHITECTURE.md §7. Does NOT include "Held":
/// a hold is a claim on a slot before an Appointment row exists at all (see
/// <see cref="AppointmentHold"/> and <see cref="HoldStatus"/>); an Appointment
/// is first created (in <see cref="PaymentPending"/>) at checkout time.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>Appointment intent exists, payment initiated, awaiting provider confirmation.</summary>
    PaymentPending = 1,

    /// <summary>Payment verified server-side — first state that counts as a real, confirmed booking.</summary>
    Scheduled = 2,

    /// <summary>Derived once the slot's end time has passed with no cancellation.</summary>
    Completed = 3,

    /// <summary>Distinct from Completed: reachable only from Scheduled, set by doctor-side action.</summary>
    NoShow = 4,

    /// <summary>Cancelled by the patient — kept distinct from Doctor/Admin since refund policy and notification copy differ by initiator.</summary>
    CancelledByPatient = 5,

    /// <summary>Cancelled by the doctor.</summary>
    CancelledByDoctor = 6,

    /// <summary>Cancelled by an admin.</summary>
    CancelledByAdmin = 7,

    /// <summary>Reachable only from a Cancelled* status, when the cancellation policy entitles the patient to a refund.</summary>
    RefundPending = 8,

    /// <summary>Refund completed by the payment provider.</summary>
    Refunded = 9,

    /// <summary>Payment was attempted but did not succeed.</summary>
    PaymentFailed = 10,

    /// <summary>Payment session/hold expired before payment completed.</summary>
    Expired = 11,
}
