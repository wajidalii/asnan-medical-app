using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// One checkout attempt against <see cref="Appointment"/> — ARCHITECTURE.md
/// §8. Created at checkout time, settled (or failed) exclusively by a
/// verified provider webhook — never by client-reported success.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public string ProviderSessionId { get; set; } = null!;

    /// <summary>Populated once the provider reports a settled outcome (webhook), null while Pending.</summary>
    public string? ProviderTransactionId { get; set; }

    public string RedirectUrl { get; set; } = null!;

    /// <summary>
    /// The idempotency key handed to the payment provider at checkout — the
    /// raw hold token, per ARCHITECTURE.md §8. Stored in plaintext
    /// deliberately (unlike hold/refresh/signup tokens, which are stored
    /// hashed): the provider must be given, and later echoes back, this
    /// exact raw value for correlation, and a leaked value here grants no
    /// capability beyond what the already-authenticated owning patient
    /// already has (checkout is idempotent on it — see PaymentService).
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    public string? FailureReason { get; set; }
}
