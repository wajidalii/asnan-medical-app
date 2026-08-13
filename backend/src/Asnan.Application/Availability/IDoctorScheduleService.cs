using Asnan.Application.Common;

namespace Asnan.Application.Availability;

public interface IDoctorScheduleService
{
    Task<ScheduleListResult> GetAllAsync(Guid doctorId, CallerContext caller, CancellationToken cancellationToken = default);

    Task<ScheduleMutationResult> CreateAsync(Guid doctorId, CreateDoctorScheduleDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    Task<ScheduleMutationResult> UpdateAsync(Guid doctorId, Guid scheduleId, UpdateDoctorScheduleDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    Task<ScheduleMutationResult> DeleteAsync(Guid doctorId, Guid scheduleId, CallerContext caller, CancellationToken cancellationToken = default);
}
