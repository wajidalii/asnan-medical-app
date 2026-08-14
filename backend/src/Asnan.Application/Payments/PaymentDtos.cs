namespace Asnan.Application.Payments;

/// <summary>Request to open a payment session for a given amount — ARCHITECTURE.md §8.</summary>
public record PaymentRequest(string IdempotencyKey, decimal Amount, string Currency, IReadOnlyDictionary<string, string> Metadata);

/// <summary>Provider-created checkout session the client is handed off to.</summary>
public record PaymentSession(string ProviderSessionId, string RedirectUrl, string IdempotencyKey);

/// <summary>A provider webhook call as received at the HTTP boundary — deliberately not `HttpRequest` so Application stays framework-agnostic and this is trivially unit-testable.</summary>
public record WebhookRequest(string RawBody, IReadOnlyDictionary<string, string> Headers);

public enum PaymentEventOutcome
{
    Succeeded = 1,
    Failed = 2,
}

/// <summary>
/// Result of verifying+parsing a webhook call. <see cref="IsValidSignature"/>
/// must be checked first — every other field is meaningless when it's false.
/// </summary>
public record PaymentVerificationResult(
    bool IsValidSignature,
    string ProviderEventId,
    string IdempotencyKey,
    PaymentEventOutcome Outcome,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    string? FailureReason);

public record RefundResult(bool Success, string? ProviderRefundId, string? FailureReason);
