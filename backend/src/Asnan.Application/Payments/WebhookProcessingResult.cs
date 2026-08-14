namespace Asnan.Application.Payments;

public enum ProcessWebhookStatus
{
    /// <summary>State changed (or a Succeeded/Failed outcome was recorded) as a result of this call.</summary>
    Processed,

    /// <summary>Same providerEventId already handled by an earlier delivery — a deliberate no-op.</summary>
    AlreadyProcessed,

    InvalidSignature,

    /// <summary>Signature was valid but no PaymentTransaction matches the payload's idempotency key.</summary>
    UnknownTransaction,
}

public record ProcessWebhookResult(ProcessWebhookStatus Status);
