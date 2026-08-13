using FluentValidation;

namespace Asnan.Application.Availability;

public record CreateHoldDto(Guid DoctorId, DateTime SlotStartUtc, DateTime SlotEndUtc);

public record HoldDto(Guid Id, Guid DoctorId, DateTime SlotStartUtc, DateTime SlotEndUtc, string HoldToken, DateTime ExpiresAtUtc);

public class CreateHoldDtoValidator : AbstractValidator<CreateHoldDto>
{
    public CreateHoldDtoValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.SlotStartUtc).LessThan(x => x.SlotEndUtc).WithMessage("Slot start must be before slot end.");
        RuleFor(x => x).Must(x => x.SlotStartUtc > DateTime.UtcNow).WithMessage("The slot must be in the future.").WithName("SlotStartUtc");
    }
}
