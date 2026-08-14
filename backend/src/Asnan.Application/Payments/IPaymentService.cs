namespace Asnan.Application.Payments;

/// <summary>
/// Checkout + webhook processing — ARCHITECTURE.md §8. The use-case layer
/// on top of <see cref="IPaymentProvider"/>: turns a hold into a paid,
/// Scheduled appointment (or a safely-failed one), never trusting anything
/// but a verified webhook to make that call.
/// </summary>
public interface IPaymentService
{
    Task<CreateCheckoutResult> CreateCheckoutAsync(Guid patientUserId, CreateCheckoutDto dto, CancellationToken cancellationToken = default);

    Task<ProcessWebhookResult> ProcessWebhookAsync(WebhookRequest request, CancellationToken cancellationToken = default);
}
