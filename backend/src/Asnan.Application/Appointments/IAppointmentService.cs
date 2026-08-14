using Asnan.Application.Common;

namespace Asnan.Application.Appointments;

/// <summary>Appointment listing + patient/doctor(/admin) self-service cancellation — issue #24.</summary>
public interface IAppointmentService
{
    /// <summary>Object-level authorized to the caller's own appointments (as patient or doctor) — never another user's.</summary>
    Task<PagedResult<AppointmentSummaryDto>> ListAsync(Guid callerId, AppointmentListQuery query, CancellationToken cancellationToken = default);

    Task<CancelAppointmentResult> CancelAsync(Guid appointmentId, Guid callerId, bool callerIsAdmin, RequestCancelAppointmentDto dto, CancellationToken cancellationToken = default);
}
