namespace Asnan.Application.Availability;

public enum ScheduleQueryStatus
{
    Success,
    DoctorNotFound,
    Forbidden,
}

public record ScheduleListResult(ScheduleQueryStatus Status, List<DoctorScheduleDto>? Schedules = null);

public enum ScheduleMutationStatus
{
    Success,
    DoctorNotFound,
    Forbidden,
    NotFound,
    OverlappingWindow,
}

public record ScheduleMutationResult(ScheduleMutationStatus Status, DoctorScheduleDto? Schedule = null);
