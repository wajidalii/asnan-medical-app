namespace Asnan.Infrastructure.Payments;

public enum MockPaymentSessionStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
}

/// <summary>In-memory record of a session the mock provider created — dev/staging only, reset on app restart (acceptable for a mock).</summary>
public class MockPaymentSession
{
    public required string ProviderSessionId { get; init; }

    public required string IdempotencyKey { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string ProviderTransactionId { get; init; }

    public MockPaymentSessionStatus Status { get; set; } = MockPaymentSessionStatus.Pending;

    /// <summary>Set once, the first time the session is confirmed — re-confirming an already-terminal session replays this same delivery rather than minting a new event (a real provider wouldn't re-fire a fresh event for an already-settled session either).</summary>
    public MockWebhookDelivery? Delivery { get; set; }
}

public record MockWebhookDelivery(string ProviderEventId, string RawBody, string SignatureHeaderValue);
