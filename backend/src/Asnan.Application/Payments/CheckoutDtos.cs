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

public record CheckoutDto(Guid AppointmentId, Guid PaymentTransactionId, string ProviderSessionId, string RedirectUrl, decimal Amount, string Currency);

public enum CreateCheckoutStatus
{
    Success,

    /// <summary>No Active hold matches this token for the calling patient.</summary>
    HoldNotFound,

    /// <summary>The hold exists but is no longer Active or has passed its expiry.</summary>
    HoldExpired,
}

public record CreateCheckoutResult(CreateCheckoutStatus Status, CheckoutDto? Checkout = null);
