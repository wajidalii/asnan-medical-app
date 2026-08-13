using FluentValidation;

namespace Asnan.Application.Auth;

public record RefreshDto(string RefreshToken);

public record LogoutDto(string RefreshToken);

public class RefreshDtoValidator : AbstractValidator<RefreshDto>
{
    public RefreshDtoValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutDtoValidator : AbstractValidator<LogoutDto>
{
    public LogoutDtoValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
