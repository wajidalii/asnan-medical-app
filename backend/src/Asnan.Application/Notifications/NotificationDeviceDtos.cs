using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Notifications;

public record RegisterDeviceDto(string FcmToken, DevicePlatform Platform);

public class RegisterDeviceDtoValidator : AbstractValidator<RegisterDeviceDto>
{
    public RegisterDeviceDtoValidator()
    {
        RuleFor(x => x.FcmToken).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Platform).IsInEnum();
    }
}

public record RemoveDeviceDto(string FcmToken);

public class RemoveDeviceDtoValidator : AbstractValidator<RemoveDeviceDto>
{
    public RemoveDeviceDtoValidator()
    {
        RuleFor(x => x.FcmToken).NotEmpty().MaximumLength(512);
    }
}
