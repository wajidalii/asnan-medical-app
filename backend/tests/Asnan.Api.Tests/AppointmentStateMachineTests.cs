using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Domain.Exceptions;

namespace Asnan.Api.Tests;

/// <summary>
/// Pure unit tests for <see cref="AppointmentStateMachine"/> (issue #23) —
/// enumerates every valid transition and asserts every other transition is
/// rejected, per the issue's testing requirement.
/// </summary>
public class AppointmentStateMachineTests
{
    private static readonly AppointmentStatus[] AllStatuses = Enum.GetValues<AppointmentStatus>();

    private static Appointment NewAppointment(AppointmentStatus status, DateTime? slotEndUtc = null) => new()
    {
        Status = status,
        SlotStartUtc = DateTime.UtcNow,
        SlotEndUtc = slotEndUtc ?? DateTime.UtcNow.AddMinutes(30),
        ConsultationFee = 100m,
        Currency = "USD",
    };

    public static IEnumerable<object[]> ValidTransitions()
    {
        yield return new object[] { AppointmentStatus.PaymentPending, AppointmentStatus.Scheduled };
        yield return new object[] { AppointmentStatus.PaymentPending, AppointmentStatus.PaymentFailed };
        yield return new object[] { AppointmentStatus.PaymentPending, AppointmentStatus.Expired };
        yield return new object[] { AppointmentStatus.Scheduled, AppointmentStatus.Completed };
        yield return new object[] { AppointmentStatus.Scheduled, AppointmentStatus.NoShow };
        yield return new object[] { AppointmentStatus.Scheduled, AppointmentStatus.CancelledByPatient };
        yield return new object[] { AppointmentStatus.Scheduled, AppointmentStatus.CancelledByDoctor };
        yield return new object[] { AppointmentStatus.Scheduled, AppointmentStatus.CancelledByAdmin };
        yield return new object[] { AppointmentStatus.CancelledByPatient, AppointmentStatus.RefundPending };
        yield return new object[] { AppointmentStatus.CancelledByDoctor, AppointmentStatus.RefundPending };
        yield return new object[] { AppointmentStatus.CancelledByAdmin, AppointmentStatus.RefundPending };
        yield return new object[] { AppointmentStatus.RefundPending, AppointmentStatus.Refunded };
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void IsValidTransition_ForEveryDocumentedTransition_ReturnsTrue(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.True(AppointmentStateMachine.IsValidTransition(from, to));
    }

    public static IEnumerable<object[]> AllStatusPairs()
    {
        foreach (var from in AllStatuses)
        {
            foreach (var to in AllStatuses)
            {
                yield return new object[] { from, to };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllStatusPairs))]
    public void IsValidTransition_ForEveryUndocumentedPair_ReturnsFalse(AppointmentStatus from, AppointmentStatus to)
    {
        var isDocumented = ValidTransitions().Any(t => (AppointmentStatus)t[0] == from && (AppointmentStatus)t[1] == to);

        Assert.Equal(isDocumented, AppointmentStateMachine.IsValidTransition(from, to));
    }

    [Fact]
    public void MarkScheduled_FromPaymentPending_UpdatesStatusAndRecordsHistory()
    {
        var appointment = NewAppointment(AppointmentStatus.PaymentPending);
        var now = DateTime.UtcNow;

        var history = AppointmentStateMachine.MarkScheduled(appointment, now);

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(AppointmentStatus.PaymentPending, history.FromStatus);
        Assert.Equal(AppointmentStatus.Scheduled, history.ToStatus);
        Assert.Equal(now, history.ChangedAtUtc);
    }

    [Fact]
    public void MarkScheduled_FromNonPaymentPending_ThrowsInvalidAppointmentTransitionException()
    {
        var appointment = NewAppointment(AppointmentStatus.Scheduled);

        var ex = Assert.Throws<InvalidAppointmentTransitionException>(() => AppointmentStateMachine.MarkScheduled(appointment, DateTime.UtcNow));

        Assert.Equal(AppointmentStatus.Scheduled, ex.From);
        Assert.Equal(AppointmentStatus.Scheduled, ex.To);
    }

    [Fact]
    public void Cancel_WithNonCancelledStatus_ThrowsArgumentOutOfRangeException()
    {
        var appointment = NewAppointment(AppointmentStatus.Scheduled);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AppointmentStateMachine.Cancel(appointment, AppointmentStatus.Scheduled, Guid.NewGuid(), null, DateTime.UtcNow));
    }

    [Fact]
    public void Cancel_ByPatientFromScheduled_Succeeds()
    {
        var appointment = NewAppointment(AppointmentStatus.Scheduled);
        var patientId = Guid.NewGuid();

        var history = AppointmentStateMachine.Cancel(appointment, AppointmentStatus.CancelledByPatient, patientId, "Changed my mind", DateTime.UtcNow);

        Assert.Equal(AppointmentStatus.CancelledByPatient, appointment.Status);
        Assert.Equal(patientId, history.ChangedByUserId);
        Assert.Equal("Changed my mind", history.Reason);
    }

    [Fact]
    public void MarkNoShow_FromNonScheduled_ThrowsInvalidAppointmentTransitionException()
    {
        var appointment = NewAppointment(AppointmentStatus.PaymentPending);

        Assert.Throws<InvalidAppointmentTransitionException>(() =>
            AppointmentStateMachine.MarkNoShow(appointment, Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void TryAutoComplete_ScheduledWithSlotEndInPast_TransitionsToCompleted()
    {
        var appointment = NewAppointment(AppointmentStatus.Scheduled, DateTime.UtcNow.AddMinutes(-1));

        var completed = AppointmentStateMachine.TryAutoComplete(appointment, DateTime.UtcNow, out var history);

        Assert.True(completed);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.NotNull(history);
        Assert.Equal(AppointmentStatus.Completed, history!.ToStatus);
    }

    [Fact]
    public void TryAutoComplete_ScheduledWithSlotEndInFuture_DoesNothing()
    {
        var appointment = NewAppointment(AppointmentStatus.Scheduled, DateTime.UtcNow.AddMinutes(30));

        var completed = AppointmentStateMachine.TryAutoComplete(appointment, DateTime.UtcNow, out var history);

        Assert.False(completed);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Null(history);
    }

    [Fact]
    public void TryAutoComplete_NonScheduledStatus_DoesNothingEvenIfSlotEndPassed()
    {
        var appointment = NewAppointment(AppointmentStatus.NoShow, DateTime.UtcNow.AddMinutes(-30));

        var completed = AppointmentStateMachine.TryAutoComplete(appointment, DateTime.UtcNow, out var history);

        Assert.False(completed);
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.Null(history);
    }

    [Fact]
    public void FullLifecycle_PaymentPendingThroughRefunded_AllTransitionsSucceed()
    {
        var appointment = NewAppointment(AppointmentStatus.PaymentPending);
        var now = DateTime.UtcNow;

        AppointmentStateMachine.MarkScheduled(appointment, now);
        AppointmentStateMachine.Cancel(appointment, AppointmentStatus.CancelledByDoctor, Guid.NewGuid(), "Doctor unavailable", now);
        AppointmentStateMachine.MarkRefundPending(appointment, now);
        AppointmentStateMachine.MarkRefunded(appointment, now);

        Assert.Equal(AppointmentStatus.Refunded, appointment.Status);
    }
}
