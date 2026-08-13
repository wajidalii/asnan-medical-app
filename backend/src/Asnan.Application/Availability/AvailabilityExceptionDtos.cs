using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Availability;

public record AvailabilityExceptionDto(Guid Id, DateOnly Date, AvailabilityExceptionType Type, TimeOnly? StartTime, TimeOnly? EndTime, string? Reason);

public record CreateAvailabilityExceptionDto(DateOnly Date, AvailabilityExceptionType Type, TimeOnly? StartTime, TimeOnly? EndTime, string? Reason);

public record UpdateAvailabilityExceptionDto(DateOnly Date, AvailabilityExceptionType Type, TimeOnly? StartTime, TimeOnly? EndTime, string? Reason);

public class CreateAvailabilityExceptionDtoValidator : AbstractValidator<CreateAvailabilityExceptionDto>
{
    public CreateAvailabilityExceptionDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(512);

        RuleFor(x => x.StartTime)
            .NotNull()
            .When(x => x.Type == AvailabilityExceptionType.ExtraAvailability)
            .WithMessage("Start time is required for extra availability.");
        RuleFor(x => x.EndTime)
            .NotNull()
            .When(x => x.Type == AvailabilityExceptionType.ExtraAvailability)
            .WithMessage("End time is required for extra availability.");

        RuleFor(x => x)
            .Must(x => x.StartTime.HasValue == x.EndTime.HasValue)
            .WithMessage("Start and end time must both be set or both left empty.")
            .WithName("StartTime");
        RuleFor(x => x)
            .Must(x => !x.StartTime.HasValue || !x.EndTime.HasValue || x.StartTime < x.EndTime)
            .WithMessage("Start time must be before end time.")
            .WithName("StartTime");
    }
}

public class UpdateAvailabilityExceptionDtoValidator : AbstractValidator<UpdateAvailabilityExceptionDto>
{
    public UpdateAvailabilityExceptionDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(512);

        RuleFor(x => x.StartTime)
            .NotNull()
            .When(x => x.Type == AvailabilityExceptionType.ExtraAvailability)
            .WithMessage("Start time is required for extra availability.");
        RuleFor(x => x.EndTime)
            .NotNull()
            .When(x => x.Type == AvailabilityExceptionType.ExtraAvailability)
            .WithMessage("End time is required for extra availability.");

        RuleFor(x => x)
            .Must(x => x.StartTime.HasValue == x.EndTime.HasValue)
            .WithMessage("Start and end time must both be set or both left empty.")
            .WithName("StartTime");
        RuleFor(x => x)
            .Must(x => !x.StartTime.HasValue || !x.EndTime.HasValue || x.StartTime < x.EndTime)
            .WithMessage("Start time must be before end time.")
            .WithName("StartTime");
    }
}
