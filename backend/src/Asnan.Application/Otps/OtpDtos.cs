using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Otps;

public record OtpRequestDto(string Destination, OtpChannel Channel, OtpPurpose Purpose);

public record OtpVerifyDto(string Destination, string Code, OtpPurpose Purpose);

public class OtpRequestDtoValidator : AbstractValidator<OtpRequestDto>
{
    public OtpRequestDtoValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
    }
}

public class OtpVerifyDtoValidator : AbstractValidator<OtpVerifyDto>
{
    public OtpVerifyDtoValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{4,8}$");
        RuleFor(x => x.Purpose).IsInEnum();
    }
}
