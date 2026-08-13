using FluentValidation;

namespace Asnan.Application.Auth;

public record LoginDto(string Identifier, string Password, string DeviceId, string? DeviceName);

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DeviceName).MaximumLength(128);
    }
}
