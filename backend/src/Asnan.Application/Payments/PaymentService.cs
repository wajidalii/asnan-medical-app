using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Payments;

public class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentProvider _paymentProvider;

    public PaymentService(IApplicationDbContext db, IPaymentProvider paymentProvider)
    {
        _db = db;
        _paymentProvider = paymentProvider;
    }

    public async Task<CreateCheckoutResult> CreateCheckoutAsync(Guid patientUserId, CreateCheckoutDto dto, CancellationToken cancellationToken = default)
    {
        var tokenHash = OpaqueTokenGenerator.Hash(dto.HoldToken);
        var hold = await _db.AppointmentHolds
            .FirstOrDefaultAsync(h => h.HoldTokenHash == tokenHash && h.PatientUserId == patientUserId, cancellationToken);
        if (hold is null)
        {
            return new CreateCheckoutResult(CreateCheckoutStatus.HoldNotFound);
        }

        // Idempotent replay: this hold was already checked out (e.g. the
        // client retried after a dropped response) — hand back the same
        // session rather than minting a second one against the same hold.
        var existingAppointment = await _db.Appointments.FirstOrDefaultAsync(a => a.SourceHoldId == hold.Id, cancellationToken);
        if (existingAppointment is not null)
        {
            var existingTransaction = await _db.PaymentTransactions.FirstAsync(t => t.AppointmentId == existingAppointment.Id, cancellationToken);
            return new CreateCheckoutResult(CreateCheckoutStatus.Success, ToCheckoutDto(existingAppointment, existingTransaction));
        }

        var now = DateTime.UtcNow;
        if (hold.Status != HoldStatus.Active || hold.ExpiresAtUtc <= now)
        {
            return new CreateCheckoutResult(CreateCheckoutStatus.HoldExpired);
        }

        var doctor = await _db.DoctorProfiles.FirstAsync(d => d.Id == hold.DoctorProfileId, cancellationToken);

        var appointment = new Appointment
        {
            DoctorProfileId = hold.DoctorProfileId,
            PatientUserId = patientUserId,
            SlotStartUtc = hold.SlotStartUtc,
            SlotEndUtc = hold.SlotEndUtc,
            Status = AppointmentStatus.PaymentPending,
            ConsultationFee = doctor.ConsultationFee,
            Currency = doctor.Currency,
            SourceHoldId = hold.Id,
        };
        _db.Appointments.Add(appointment);
        _db.AppointmentStatusHistories.Add(new AppointmentStatusHistory
        {
            AppointmentId = appointment.Id,
            FromStatus = null,
            ToStatus = AppointmentStatus.PaymentPending,
            ChangedByUserId = patientUserId,
            Reason = "Checkout initiated.",
            ChangedAtUtc = now,
        });

        var session = await _paymentProvider.CreateSessionAsync(
            new PaymentRequest(dto.HoldToken, doctor.ConsultationFee, doctor.Currency, new Dictionary<string, string> { ["appointmentId"] = appointment.Id.ToString() }),
            cancellationToken);

        var transaction = new PaymentTransaction
        {
            AppointmentId = appointment.Id,
            ProviderSessionId = session.ProviderSessionId,
            RedirectUrl = session.RedirectUrl,
            IdempotencyKey = dto.HoldToken,
            Amount = doctor.ConsultationFee,
            Currency = doctor.Currency,
            Status = PaymentTransactionStatus.Pending,
        };
        _db.PaymentTransactions.Add(transaction);

        await _db.SaveChangesAsync(cancellationToken);

        return new CreateCheckoutResult(CreateCheckoutStatus.Success, ToCheckoutDto(appointment, transaction));
    }

    public async Task<ProcessWebhookResult> ProcessWebhookAsync(WebhookRequest request, CancellationToken cancellationToken = default)
    {
        var verification = await _paymentProvider.VerifyWebhookAsync(request, cancellationToken);
        if (!verification.IsValidSignature)
        {
            return new ProcessWebhookResult(ProcessWebhookStatus.InvalidSignature);
        }

        var alreadyProcessed = await _db.ProcessedWebhookEvents.AnyAsync(e => e.ProviderEventId == verification.ProviderEventId, cancellationToken);
        if (alreadyProcessed)
        {
            return new ProcessWebhookResult(ProcessWebhookStatus.AlreadyProcessed);
        }

        var transaction = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.IdempotencyKey == verification.IdempotencyKey, cancellationToken);
        if (transaction is null)
        {
            return new ProcessWebhookResult(ProcessWebhookStatus.UnknownTransaction);
        }

        var appointment = await _db.Appointments.FirstAsync(a => a.Id == transaction.AppointmentId, cancellationToken);
        var now = DateTime.UtcNow;

        if (verification.Outcome == PaymentEventOutcome.Failed)
        {
            await HandleFailedPaymentAsync(appointment, transaction, verification, now, cancellationToken);
        }
        else
        {
            await HandleSucceededPaymentAsync(appointment, transaction, verification, now, cancellationToken);
        }

        _db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent { ProviderEventId = verification.ProviderEventId, ProcessedAtUtc = now });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race to a concurrent delivery of the same event — the winner already recorded it.
            return new ProcessWebhookResult(ProcessWebhookStatus.AlreadyProcessed);
        }

        return new ProcessWebhookResult(ProcessWebhookStatus.Processed);
    }

    private async Task HandleFailedPaymentAsync(Appointment appointment, PaymentTransaction transaction, PaymentVerificationResult verification, DateTime now, CancellationToken cancellationToken)
    {
        transaction.Status = PaymentTransactionStatus.Failed;
        transaction.FailureReason = verification.FailureReason;
        transaction.ProviderTransactionId = verification.ProviderTransactionId;

        if (appointment.Status != AppointmentStatus.PaymentPending)
        {
            return;
        }

        var history = AppointmentStateMachine.MarkPaymentFailed(appointment, verification.FailureReason ?? "Payment failed.", now);
        _db.AppointmentStatusHistories.Add(history);

        var hold = await _db.AppointmentHolds.FirstOrDefaultAsync(h => h.Id == appointment.SourceHoldId, cancellationToken);
        if (hold is not null && hold.Status == HoldStatus.Active)
        {
            hold.Status = HoldStatus.Released;
            hold.ActiveSlotKey = null;
        }
    }

    private async Task HandleSucceededPaymentAsync(Appointment appointment, PaymentTransaction transaction, PaymentVerificationResult verification, DateTime now, CancellationToken cancellationToken)
    {
        if (appointment.Status != AppointmentStatus.PaymentPending)
        {
            // Already resolved via another path (or a stale/duplicate-in-spirit delivery) — nothing to do.
            return;
        }

        // Re-validate the hold rather than trusting that "payment succeeded" also means "the slot is still ours"
        // — ARCHITECTURE.md §8. A hold that has since expired/been released means we cannot safely honor the
        // booking even though the provider captured the money; fail the appointment instead of silently
        // scheduling or leaving an orphan. Actually issuing a refund for this case is #21's scope.
        var hold = await _db.AppointmentHolds.FirstOrDefaultAsync(h => h.Id == appointment.SourceHoldId, cancellationToken);
        var holdStillValid = hold is not null && hold.Status == HoldStatus.Active && hold.ExpiresAtUtc > now;

        transaction.Status = PaymentTransactionStatus.Succeeded;
        transaction.ProviderTransactionId = verification.ProviderTransactionId;

        if (!holdStillValid)
        {
            var failedHistory = AppointmentStateMachine.MarkPaymentFailed(appointment, "Payment captured but the slot hold had already expired; refund required.", now);
            _db.AppointmentStatusHistories.Add(failedHistory);
            return;
        }

        var scheduledHistory = AppointmentStateMachine.MarkScheduled(appointment, now);
        _db.AppointmentStatusHistories.Add(scheduledHistory);

        hold!.Status = HoldStatus.Consumed;
        hold.ActiveSlotKey = null;

        var doctor = await _db.DoctorProfiles.FirstAsync(d => d.Id == appointment.DoctorProfileId, cancellationToken);
        var conversation = new ChatConversation { AppointmentId = appointment.Id };
        _db.ChatConversations.Add(conversation);
        _db.ChatParticipants.Add(new ChatParticipant { ChatConversation = conversation, UserId = appointment.PatientUserId });
        _db.ChatParticipants.Add(new ChatParticipant { ChatConversation = conversation, UserId = doctor.UserId });
    }

    private static CheckoutDto ToCheckoutDto(Appointment appointment, PaymentTransaction transaction) =>
        new(appointment.Id, transaction.Id, transaction.ProviderSessionId, transaction.RedirectUrl, transaction.Amount, transaction.Currency, appointment.Status);
}
