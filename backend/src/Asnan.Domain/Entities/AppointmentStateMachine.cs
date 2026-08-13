using Asnan.Domain.Enums;
using Asnan.Domain.Exceptions;

namespace Asnan.Domain.Entities;

/// <summary>
/// The single source of truth for <see cref="Appointment.Status"/> changes —
/// ARCHITECTURE.md §7. Named, testable transition methods rather than
/// scattered `if` checks; every call mutates the given appointment and
/// returns the <see cref="AppointmentStatusHistory"/> row the caller must
/// add to the DbSet (this class is persistence-ignorant by design).
///
/// Transition table:
/// <code>
/// PaymentPending   -> Scheduled, PaymentFailed, Expired
/// Scheduled        -> Completed, NoShow, CancelledByPatient, CancelledByDoctor, CancelledByAdmin
/// CancelledBy*     -> RefundPending   (only when the cancellation policy entitles a refund; otherwise terminal)
/// RefundPending    -> Refunded
/// Completed, NoShow, Refunded, PaymentFailed, Expired -> (terminal)
/// </code>
/// </summary>
public static class AppointmentStateMachine
{
    private static readonly IReadOnlyDictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions =
        new Dictionary<AppointmentStatus, AppointmentStatus[]>
        {
            [AppointmentStatus.PaymentPending] = new[]
            {
                AppointmentStatus.Scheduled,
                AppointmentStatus.PaymentFailed,
                AppointmentStatus.Expired,
            },
            [AppointmentStatus.Scheduled] = new[]
            {
                AppointmentStatus.Completed,
                AppointmentStatus.NoShow,
                AppointmentStatus.CancelledByPatient,
                AppointmentStatus.CancelledByDoctor,
                AppointmentStatus.CancelledByAdmin,
            },
            [AppointmentStatus.CancelledByPatient] = new[] { AppointmentStatus.RefundPending },
            [AppointmentStatus.CancelledByDoctor] = new[] { AppointmentStatus.RefundPending },
            [AppointmentStatus.CancelledByAdmin] = new[] { AppointmentStatus.RefundPending },
            [AppointmentStatus.RefundPending] = new[] { AppointmentStatus.Refunded },
        };

    public static bool IsValidTransition(AppointmentStatus from, AppointmentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static AppointmentStatusHistory MarkScheduled(Appointment appointment, DateTime nowUtc) =>
        Transition(appointment, AppointmentStatus.Scheduled, changedByUserId: null, "Payment verified.", nowUtc);

    public static AppointmentStatusHistory MarkPaymentFailed(Appointment appointment, string reason, DateTime nowUtc) =>
        Transition(appointment, AppointmentStatus.PaymentFailed, changedByUserId: null, reason, nowUtc);

    public static AppointmentStatusHistory MarkExpired(Appointment appointment, DateTime nowUtc) =>
        Transition(appointment, AppointmentStatus.Expired, changedByUserId: null, "Payment session expired.", nowUtc);

    /// <summary>Reachable only from Scheduled — enforced by the transition table, not a separate check.</summary>
    public static AppointmentStatusHistory MarkNoShow(Appointment appointment, Guid changedByUserId, DateTime nowUtc) =>
        Transition(appointment, AppointmentStatus.NoShow, changedByUserId, "Patient did not show up.", nowUtc);

    /// <summary>
    /// Automatic, computed-on-read completion: call sites that load a
    /// Scheduled appointment whose slot has ended should call this (and
    /// persist the result) before returning it — no background job for v1
    /// per "do not over-engineer infrastructure initially".
    /// </summary>
    public static bool TryAutoComplete(Appointment appointment, DateTime nowUtc, out AppointmentStatusHistory? history)
    {
        if (appointment.Status == AppointmentStatus.Scheduled && appointment.SlotEndUtc <= nowUtc)
        {
            history = Transition(appointment, AppointmentStatus.Completed, changedByUserId: null, "Auto-completed: slot end time passed.", nowUtc);
            return true;
        }

        history = null;
        return false;
    }

    public static AppointmentStatusHistory Cancel(Appointment appointment, AppointmentStatus cancelledByStatus, Guid changedByUserId, string? reason, DateTime nowUtc)
    {
        if (cancelledByStatus is not (AppointmentStatus.CancelledByPatient or AppointmentStatus.CancelledByDoctor or AppointmentStatus.CancelledByAdmin))
        {
            throw new ArgumentOutOfRangeException(nameof(cancelledByStatus), cancelledByStatus, "Must be one of the CancelledBy* statuses.");
        }

        return Transition(appointment, cancelledByStatus, changedByUserId, reason, nowUtc);
    }

    /// <summary>Only valid when the cancellation policy entitles the patient to a refund — see the CancelledBy* row of the transition table.</summary>
    public static AppointmentStatusHistory MarkRefundPending(Appointment appointment, DateTime nowUtc) =>
        Transition(appointment, AppointmentStatus.RefundPending, changedByUserId: null, "Refund initiated.", nowUtc);

    public static AppointmentStatusHistory MarkRefunded(Appointment appointment, DateTime nowUtc) =>
        Transition(appointment, AppointmentStatus.Refunded, changedByUserId: null, "Refund completed.", nowUtc);

    private static AppointmentStatusHistory Transition(Appointment appointment, AppointmentStatus to, Guid? changedByUserId, string? reason, DateTime nowUtc)
    {
        var from = appointment.Status;
        if (!IsValidTransition(from, to))
        {
            throw new InvalidAppointmentTransitionException(from, to);
        }

        appointment.Status = to;
        appointment.UpdatedAtUtc = nowUtc;

        return new AppointmentStatusHistory
        {
            AppointmentId = appointment.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = changedByUserId,
            Reason = reason,
            ChangedAtUtc = nowUtc,
        };
    }
}
