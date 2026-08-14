using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// One refund attempt against a settled <see cref="PaymentTransaction"/> —
/// ARCHITECTURE.md §"Payment"/"Appointment Cancellation". Created when an
/// appointment's cancellation entitles the patient to money back; the
/// <see cref="Appointment"/> itself tracks RefundPending/Refunded via
/// <see cref="AppointmentStateMachine"/>, this row is the durable record of
/// the actual provider-side attempt (amount, outcome, provider reference).
/// </summary>
public class Refund : BaseEntity
{
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public Guid PaymentTransactionId { get; set; }

    public PaymentTransaction PaymentTransaction { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public string? Reason { get; set; }

    /// <summary>Populated once the provider confirms the refund; null while Pending/Failed.</summary>
    public string? ProviderRefundId { get; set; }

    public string? FailureReason { get; set; }
}
