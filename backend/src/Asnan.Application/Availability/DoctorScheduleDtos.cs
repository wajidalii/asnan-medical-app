using FluentValidation;

namespace Asnan.Application.Availability;

public record DoctorScheduleDto(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes, int BufferMinutes);

public record CreateDoctorScheduleDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes, int BufferMinutes = 0);

public record UpdateDoctorScheduleDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes, int BufferMinutes = 0);

public class CreateDoctorScheduleDtoValidator : AbstractValidator<CreateDoctorScheduleDto>
{
    public CreateDoctorScheduleDtoValidator()
    {
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime).WithMessage("Start time must be before end time.");
        RuleFor(x => x.SlotDurationMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.BufferMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => (x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan()) >= TimeSpan.FromMinutes(x.SlotDurationMinutes))
            .WithMessage("The schedule window must be at least as long as one slot duration.")
            .WithName("SlotDurationMinutes");
    }
}

public class UpdateDoctorScheduleDtoValidator : AbstractValidator<UpdateDoctorScheduleDto>
{
    public UpdateDoctorScheduleDtoValidator()
    {
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime).WithMessage("Start time must be before end time.");
        RuleFor(x => x.SlotDurationMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.BufferMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => (x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan()) >= TimeSpan.FromMinutes(x.SlotDurationMinutes))
            .WithMessage("The schedule window must be at least as long as one slot duration.")
            .WithName("SlotDurationMinutes");
    }
}
