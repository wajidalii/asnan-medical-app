namespace Asnan.Application.Doctors;

public enum DoctorMutationStatus
{
    Success,
    UserNotFound,
    ProfileAlreadyExists,
    NotFound,
    SpecialtyNotFound,
}

public record DoctorMutationResult(DoctorMutationStatus Status, DoctorProfileDto? Doctor = null);
