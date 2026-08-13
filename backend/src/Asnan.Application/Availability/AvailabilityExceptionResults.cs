namespace Asnan.Application.Availability;

public enum AvailabilityExceptionQueryStatus
{
    Success,
    DoctorNotFound,
    Forbidden,
}

public record AvailabilityExceptionListResult(AvailabilityExceptionQueryStatus Status, List<AvailabilityExceptionDto>? Exceptions = null);

public enum AvailabilityExceptionMutationStatus
{
    Success,
    DoctorNotFound,
    Forbidden,
    NotFound,
    Conflict,
}

public record AvailabilityExceptionMutationResult(AvailabilityExceptionMutationStatus Status, AvailabilityExceptionDto? Exception = null);
