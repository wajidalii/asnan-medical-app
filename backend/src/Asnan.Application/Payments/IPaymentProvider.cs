namespace Asnan.Application.Payments;

/// <summary>
/// Payment provider abstraction — ARCHITECTURE.md §8. Selected via DI/config
/// (see AddInfrastructure) so the concrete provider (mock today, a real
/// provider such as Stripe later — see issue #60) is swappable without
/// touching any calling code. Client-reported "payment succeeded" is never
/// trusted; only a verified webhook (<see cref="VerifyWebhookAsync"/>) may
/// change appointment state.
/// </summary>
public interface IPaymentProvider
{
    Task<PaymentSession> CreateSessionAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyWebhookAsync(WebhookRequest request, CancellationToken cancellationToken = default);

    Task<RefundResult> RefundAsync(string providerTransactionId, decimal amount, CancellationToken cancellationToken = default);
}
