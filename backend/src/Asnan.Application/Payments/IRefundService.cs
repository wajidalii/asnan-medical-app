namespace Asnan.Application.Payments;

/// <summary>
/// Admin/system-triggered cancel-and-refund — issue #21. Cancels a
/// Scheduled appointment and, if it has a captured payment, refunds it via
/// <see cref="IPaymentProvider"/>. Patient/doctor self-service cancellation
/// with windowed policy checks is a separate, later concern (Milestone 6)
/// that will call the same underlying mechanism with a computed percentage.
/// </summary>
public interface IRefundService
{
    Task<CancelAndRefundResult> CancelAndRefundAsync(Guid appointmentId, Guid initiatedByUserId, CancelAppointmentDto dto, CancellationToken cancellationToken = default);
}
