namespace Asnan.Application.Availability;

public interface IAppointmentHoldService
{
    Task<CreateHoldResult> CreateAsync(Guid patientUserId, CreateHoldDto dto, CancellationToken cancellationToken = default);
}
