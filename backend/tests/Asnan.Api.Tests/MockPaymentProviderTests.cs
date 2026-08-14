using Asnan.Application.Payments;
using Asnan.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// Pure unit tests for <see cref="MockPaymentProvider"/> (issue #19) — no
/// database/HTTP involved, per the issue's testing requirement ("unit tests
/// for the mock provider's session-creation and webhook-verification behavior").
/// </summary>
public class MockPaymentProviderTests
{
    private static MockPaymentProvider CreateProvider(string signingKey = "test-signing-key") =>
        new(new MockPaymentProviderStore(), Options.Create(new PaymentOptions { MockWebhookSigningKey = signingKey }));

    private static PaymentRequest Request(decimal amount = 100m, string currency = "USD") =>
        new($"idem-{Guid.NewGuid()}", amount, currency, new Dictionary<string, string>());

    [Fact]
    public async Task CreateSessionAsync_ReturnsSessionCarryingTheIdempotencyKey()
    {
        var provider = CreateProvider();
        var request = Request();

        var session = await provider.CreateSessionAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(session.ProviderSessionId));
        Assert.Equal(request.IdempotencyKey, session.IdempotencyKey);
    }

    [Fact]
    public async Task VerifyWebhookAsync_ForConfirmedSuccessfulSession_ReturnsSucceededWithMatchingDetails()
    {
        var provider = CreateProvider();
        var request = Request(150m, "USD");
        var session = await provider.CreateSessionAsync(request);

        var delivery = ((IMockPaymentProviderConfirmation)provider).Confirm(session.ProviderSessionId, succeeded: true, failureReason: null);
        Assert.NotNull(delivery);

        var result = await provider.VerifyWebhookAsync(new WebhookRequest(delivery!.RawBody, new Dictionary<string, string> { ["X-Mock-Signature"] = delivery.SignatureHeaderValue }));

        Assert.True(result.IsValidSignature);
        Assert.Equal(PaymentEventOutcome.Succeeded, result.Outcome);
        Assert.Equal(request.IdempotencyKey, result.IdempotencyKey);
        Assert.Equal(150m, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(delivery.ProviderEventId, result.ProviderEventId);
    }

    [Fact]
    public async Task VerifyWebhookAsync_ForConfirmedFailedSession_ReturnsFailedWithReason()
    {
        var provider = CreateProvider();
        var session = await provider.CreateSessionAsync(Request());

        var delivery = ((IMockPaymentProviderConfirmation)provider).Confirm(session.ProviderSessionId, succeeded: false, failureReason: "Card declined");
        Assert.NotNull(delivery);

        var result = await provider.VerifyWebhookAsync(new WebhookRequest(delivery!.RawBody, new Dictionary<string, string> { ["X-Mock-Signature"] = delivery.SignatureHeaderValue }));

        Assert.True(result.IsValidSignature);
        Assert.Equal(PaymentEventOutcome.Failed, result.Outcome);
        Assert.Equal("Card declined", result.FailureReason);
    }

    [Fact]
    public async Task VerifyWebhookAsync_WithTamperedBody_ReturnsInvalidSignature()
    {
        var provider = CreateProvider();
        var session = await provider.CreateSessionAsync(Request());
        var delivery = ((IMockPaymentProviderConfirmation)provider).Confirm(session.ProviderSessionId, succeeded: true, failureReason: null);

        var tamperedBody = delivery!.RawBody.Replace("Succeeded", "Failed");
        var result = await provider.VerifyWebhookAsync(new WebhookRequest(tamperedBody, new Dictionary<string, string> { ["X-Mock-Signature"] = delivery.SignatureHeaderValue }));

        Assert.False(result.IsValidSignature);
    }

    [Fact]
    public async Task VerifyWebhookAsync_MissingSignatureHeader_ReturnsInvalidSignature()
    {
        var provider = CreateProvider();
        var session = await provider.CreateSessionAsync(Request());
        var delivery = ((IMockPaymentProviderConfirmation)provider).Confirm(session.ProviderSessionId, succeeded: true, failureReason: null);

        var result = await provider.VerifyWebhookAsync(new WebhookRequest(delivery!.RawBody, new Dictionary<string, string>()));

        Assert.False(result.IsValidSignature);
    }

    [Fact]
    public void Confirm_ForUnknownSession_ReturnsNull()
    {
        var provider = CreateProvider();

        var delivery = ((IMockPaymentProviderConfirmation)provider).Confirm("no-such-session", succeeded: true, failureReason: null);

        Assert.Null(delivery);
    }

    [Fact]
    public async Task Confirm_CalledTwice_ReplaysTheSameDeliveryInsteadOfMintingANewEvent()
    {
        var provider = CreateProvider();
        var session = await provider.CreateSessionAsync(Request());
        var confirmation = (IMockPaymentProviderConfirmation)provider;

        var first = confirmation.Confirm(session.ProviderSessionId, succeeded: true, failureReason: null);
        var second = confirmation.Confirm(session.ProviderSessionId, succeeded: false, failureReason: "should be ignored");

        Assert.Equal(first!.ProviderEventId, second!.ProviderEventId);
        Assert.Equal(first.RawBody, second.RawBody);
    }

    [Fact]
    public async Task RefundAsync_ReturnsSuccessWithProviderRefundId()
    {
        var provider = CreateProvider();

        var result = await provider.RefundAsync("mock_txn_abc", 50m);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderRefundId));
    }
}
