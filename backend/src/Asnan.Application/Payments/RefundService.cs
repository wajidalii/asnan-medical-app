using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Payments;

public class RefundService : IRefundService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentProvider _paymentProvider;

    public RefundService(IApplicationDbContext db, IPaymentProvider paymentProvider)
    {
        _db = db;
        _paymentProvider = paymentProvider;
    }

    public async Task<CancelAndRefundResult> CancelAndRefundAsync(Guid appointmentId, AppointmentStatus cancelledByStatus, Guid initiatedByUserId, CancelAppointmentDto dto, CancellationToken cancellationToken = default)
    {
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);
        if (appointment is null)
        {
            return new CancelAndRefundResult(CancelAndRefundStatus.AppointmentNotFound);
        }

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            return new CancelAndRefundResult(CancelAndRefundStatus.NotCancellable);
        }

        var now = DateTime.UtcNow;
        var cancelHistory = AppointmentStateMachine.Cancel(appointment, cancelledByStatus, initiatedByUserId, dto.Reason, now);
        _db.AppointmentStatusHistories.Add(cancelHistory);

        var transaction = await _db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.AppointmentId == appointment.Id && t.Status == PaymentTransactionStatus.Succeeded, cancellationToken);
        if (transaction is null)
        {
            // Nothing was ever captured for this appointment (shouldn't normally happen for a
            // Scheduled appointment, but defend against it) — cancellation alone is the outcome.
            await _db.SaveChangesAsync(cancellationToken);
            return new CancelAndRefundResult(CancelAndRefundStatus.Success, new AppointmentCancellationDto(appointment.Id, appointment.Status, null, null, null));
        }

        var refundPendingHistory = AppointmentStateMachine.MarkRefundPending(appointment, now);
        _db.AppointmentStatusHistories.Add(refundPendingHistory);

        var refundAmount = Math.Round(transaction.Amount * dto.RefundPercentage / 100m, 2);
        var refund = new Refund
        {
            AppointmentId = appointment.Id,
            PaymentTransactionId = transaction.Id,
            Amount = refundAmount,
            Currency = transaction.Currency,
            Reason = dto.Reason,
            Status = RefundStatus.Pending,
        };
        _db.Refunds.Add(refund);

        var providerResult = await _paymentProvider.RefundAsync(transaction.ProviderTransactionId!, refundAmount, cancellationToken);

        if (providerResult.Success)
        {
            refund.Status = RefundStatus.Succeeded;
            refund.ProviderRefundId = providerResult.ProviderRefundId;

            var refundedHistory = AppointmentStateMachine.MarkRefunded(appointment, now);
            _db.AppointmentStatusHistories.Add(refundedHistory);
        }
        else
        {
            refund.Status = RefundStatus.Failed;
            refund.FailureReason = providerResult.FailureReason;
            // Appointment stays RefundPending — a valid, non-terminal state; a human/ops retry is needed.
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new CancelAndRefundResult(CancelAndRefundStatus.Success, new AppointmentCancellationDto(appointment.Id, appointment.Status, refund.Id, refund.Amount, refund.Status));
    }
}
