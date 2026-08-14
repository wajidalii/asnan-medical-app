namespace Asnan.Infrastructure.Payments;

/// <summary>
/// Dev/staging-only surface for simulating a provider settling a checkout
/// session — deliberately separate from <see cref="Asnan.Application.Payments.IPaymentProvider"/>
/// so this test-only capability can never leak into code written against
/// the provider-agnostic interface. Only the dev-gated confirm controller
/// depends on this.
/// </summary>
public interface IMockPaymentProviderConfirmation
{
    /// <summary>Null if no session exists with this id.</summary>
    MockWebhookDelivery? Confirm(string providerSessionId, bool succeeded, string? failureReason);
}
