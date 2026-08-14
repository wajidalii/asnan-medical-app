using Asnan.Application.Common;

namespace Asnan.Application.Appointments;

/// <summary>Appointment listing + patient/doctor(/admin) self-service cancellation — issue #24.</summary>
public interface IAppointmentService
{
    /// <summary>Object-level authorized to the caller's own appointments (as patient or doctor) — never another user's.</summary>
    Task<PagedResult<AppointmentSummaryDto>> ListAsync(Guid callerId, AppointmentListQuery query, CancellationToken cancellationToken = default);

    Task<CancelAppointmentResult> CancelAsync(Guid appointmentId, Guid callerId, bool callerIsAdmin, RequestCancelAppointmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>Read-only — computes what CancelAsync would do right now without mutating anything.</summary>
    Task<PreviewCancellationResult> PreviewCancellationAsync(Guid appointmentId, Guid callerId, bool callerIsAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Single-appointment lookup — issue #32's deep-link target (a push
    /// notification only carries an id, not the full summary the list
    /// endpoint returns). Same object-level authorization as CancelAsync.
    /// </summary>
    Task<GetAppointmentResult> GetByIdAsync(Guid appointmentId, Guid callerId, bool callerIsAdmin, CancellationToken cancellationToken = default);
}
