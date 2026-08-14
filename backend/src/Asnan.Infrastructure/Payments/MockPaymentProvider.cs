using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asnan.Application.Payments;
using Microsoft.Extensions.Options;

namespace Asnan.Infrastructure.Payments;

/// <summary>
/// Simulates an external payment provider entirely in-process (no real
/// credentials/network call) so the full booking→payment→scheduled flow is
/// testable end to end — ARCHITECTURE.md §8. A real provider (Stripe is the
/// leading candidate) is added later behind the same <see cref="IPaymentProvider"/>
/// interface; see issue #60.
///
/// Signature scheme is intentionally simple (HMAC-SHA256 over the raw JSON
/// body, hex-encoded, in the <c>X-Mock-Signature</c> header) — it only needs
/// to exercise the same "verify before trusting" code path a real provider's
/// scheme would, not match any particular provider's actual algorithm.
/// </summary>
public class MockPaymentProvider : IPaymentProvider, IMockPaymentProviderConfirmation
{
    private const string SignatureHeaderName = "X-Mock-Signature";

    private readonly MockPaymentProviderStore _store;
    private readonly PaymentOptions _options;

    public MockPaymentProvider(MockPaymentProviderStore store, IOptions<PaymentOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public Task<PaymentSession> CreateSessionAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var providerSessionId = $"mock_sess_{Guid.NewGuid():N}";
        var session = new MockPaymentSession
        {
            ProviderSessionId = providerSessionId,
            IdempotencyKey = request.IdempotencyKey,
            Amount = request.Amount,
            Currency = request.Currency,
            ProviderTransactionId = $"mock_txn_{Guid.NewGuid():N}",
        };
        _store.Add(session);

        return Task.FromResult(new PaymentSession(
            providerSessionId,
            $"/api/v1/payments/mock/sessions/{providerSessionId}/confirm",
            request.IdempotencyKey));
    }

    public Task<PaymentVerificationResult> VerifyWebhookAsync(WebhookRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Headers.TryGetValue(SignatureHeaderName, out var providedSignature)
            || !SignatureMatches(request.RawBody, providedSignature))
        {
            return Task.FromResult(new PaymentVerificationResult(false, "", "", PaymentEventOutcome.Failed, "", 0m, "", "Invalid or missing signature."));
        }

        var payload = JsonSerializer.Deserialize<MockWebhookEventPayload>(request.RawBody)
            ?? throw new InvalidOperationException("Mock webhook body did not deserialize.");

        var outcome = payload.Outcome == "Succeeded" ? PaymentEventOutcome.Succeeded : PaymentEventOutcome.Failed;

        return Task.FromResult(new PaymentVerificationResult(
            true,
            payload.EventId,
            payload.IdempotencyKey,
            outcome,
            payload.ProviderTransactionId,
            payload.Amount,
            payload.Currency,
            payload.FailureReason));
    }

    public Task<RefundResult> RefundAsync(string providerTransactionId, decimal amount, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RefundResult(true, $"mock_refund_{Guid.NewGuid():N}", null));
    }

    public MockWebhookDelivery? Confirm(string providerSessionId, bool succeeded, string? failureReason)
    {
        var session = _store.Find(providerSessionId);
        if (session is null)
        {
            return null;
        }

        if (session.Delivery is not null)
        {
            return session.Delivery;
        }

        session.Status = succeeded ? MockPaymentSessionStatus.Succeeded : MockPaymentSessionStatus.Failed;

        var payload = new MockWebhookEventPayload(
            EventId: $"mock_evt_{Guid.NewGuid():N}",
            IdempotencyKey: session.IdempotencyKey,
            ProviderTransactionId: session.ProviderTransactionId,
            Amount: session.Amount,
            Currency: session.Currency,
            Outcome: succeeded ? "Succeeded" : "Failed",
            FailureReason: succeeded ? null : failureReason ?? "Payment declined.");

        var rawBody = JsonSerializer.Serialize(payload);
        var delivery = new MockWebhookDelivery(payload.EventId, rawBody, Sign(rawBody));
        session.Delivery = delivery;

        return delivery;
    }

    private bool SignatureMatches(string rawBody, string providedSignature) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(rawBody)), Encoding.UTF8.GetBytes(providedSignature));

    private string Sign(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.MockWebhookSigningKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
    }

    private record MockWebhookEventPayload(
        string EventId,
        string IdempotencyKey,
        string ProviderTransactionId,
        decimal Amount,
        string Currency,
        string Outcome,
        string? FailureReason);
}
