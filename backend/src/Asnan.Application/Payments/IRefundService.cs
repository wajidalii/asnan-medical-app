using Asnan.Domain.Enums;

namespace Asnan.Application.Payments;

/// <summary>
/// The shared cancel-and-refund mechanism (issue #21) — cancels a Scheduled
/// appointment and, if it has a captured payment, refunds it via
/// <see cref="IPaymentProvider"/>. Two callers use this: the admin-only
/// endpoint (arbitrary caller-specified <paramref name="cancelledByStatus"/>
/// and refund percentage) and the patient/doctor self-service endpoint
/// (Milestone 6, issue #24 — resolves the initiator from the caller's
/// relationship to the appointment and computes the refund percentage from
/// the configurable cancellation-window policy before calling this).
/// </summary>
public interface IRefundService
{
    Task<CancelAndRefundResult> CancelAndRefundAsync(Guid appointmentId, AppointmentStatus cancelledByStatus, Guid initiatedByUserId, CancelAppointmentDto dto, CancellationToken cancellationToken = default);
}
