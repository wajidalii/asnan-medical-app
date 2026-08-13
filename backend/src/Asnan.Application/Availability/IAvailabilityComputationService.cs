namespace Asnan.Application.Availability;

public interface IAvailabilityComputationService
{
    Task<DoctorAvailabilityResult> GetAvailabilityAsync(Guid doctorId, DateOnly date, CancellationToken cancellationToken = default);
}
