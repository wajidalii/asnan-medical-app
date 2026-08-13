using Asnan.Application.Common;

namespace Asnan.Application.Availability;

public interface IAvailabilityExceptionService
{
    Task<AvailabilityExceptionListResult> GetAllAsync(Guid doctorId, CallerContext caller, CancellationToken cancellationToken = default);

    Task<AvailabilityExceptionMutationResult> CreateAsync(Guid doctorId, CreateAvailabilityExceptionDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    Task<AvailabilityExceptionMutationResult> UpdateAsync(Guid doctorId, Guid exceptionId, UpdateAvailabilityExceptionDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    Task<AvailabilityExceptionMutationResult> DeleteAsync(Guid doctorId, Guid exceptionId, CallerContext caller, CancellationToken cancellationToken = default);
}
