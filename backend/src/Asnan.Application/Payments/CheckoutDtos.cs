using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Payments;

public record CreateCheckoutDto(string HoldToken);

public class CreateCheckoutDtoValidator : AbstractValidator<CreateCheckoutDto>
{
    public CreateCheckoutDtoValidator()
    {
        RuleFor(x => x.HoldToken).NotEmpty();
    }
}

/// <summary>
/// <see cref="Status"/> lets the Flutter confirmation screen (#22) poll this
/// same idempotent endpoint to observe the appointment reaching Scheduled —
/// never trusting a client-reported payment result, per ARCHITECTURE.md §8.
/// </summary>
public record CheckoutDto(Guid AppointmentId, Guid PaymentTransactionId, string ProviderSessionId, string RedirectUrl, decimal Amount, string Currency, AppointmentStatus Status);

public enum CreateCheckoutStatus
{
    Success,

    /// <summary>No Active hold matches this token for the calling patient.</summary>
    HoldNotFound,

    /// <summary>The hold exists but is no longer Active or has passed its expiry.</summary>
    HoldExpired,
}

public record CreateCheckoutResult(CreateCheckoutStatus Status, CheckoutDto? Checkout = null);
