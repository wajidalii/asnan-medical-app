namespace Asnan.Application.Availability;

public enum DoctorAvailabilityStatus
{
    Success,
    DoctorNotFound,
}

public record DoctorAvailabilityResult(DoctorAvailabilityStatus Status, DoctorAvailabilityDto? Availability = null);
