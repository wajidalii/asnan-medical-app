using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Auth;

public record SignupRequestOtpDto(string Destination, OtpChannel Channel);

public record SignupVerifyOtpDto(string Destination, string Code, OtpChannel Channel);

public record SignupSetPasswordDto(string SignupToken, string Password);

public class SignupRequestOtpDtoValidator : AbstractValidator<SignupRequestOtpDto>
{
    public SignupRequestOtpDtoValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Channel).IsInEnum();
    }
}

public class SignupVerifyOtpDtoValidator : AbstractValidator<SignupVerifyOtpDto>
{
    public SignupVerifyOtpDtoValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{4,8}$");
        RuleFor(x => x.Channel).IsInEnum();
    }
}

public class SignupSetPasswordDtoValidator : AbstractValidator<SignupSetPasswordDto>
{
    public SignupSetPasswordDtoValidator()
    {
        RuleFor(x => x.SignupToken).NotEmpty();

        // NIST 800-63B-style policy (ARCHITECTURE.md §4.1): length + breach-list
        // check, no forced uppercase/number/symbol composition rules.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(64)
            .Must(p => !CommonPasswordChecker.IsCommon(p))
            .WithMessage("This password is too common. Please choose a different one.");
    }
}
