using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Payments;

/// <summary>
/// <see cref="RefundPercentage"/> is the mechanism's "configurable
/// percentage" knob (issue #21) — computing WHICH percentage a given
/// cancellation is entitled to (cancellation window, initiator, etc.) is
/// the appointment-cancellation issue's policy layer (Milestone 6); this
/// endpoint just refunds whatever percentage it's told to.
/// </summary>
public record CancelAppointmentDto(string? Reason, int RefundPercentage = 100);

public class CancelAppointmentDtoValidator : AbstractValidator<CancelAppointmentDto>
{
    public CancelAppointmentDtoValidator()
    {
        RuleFor(x => x.RefundPercentage).InclusiveBetween(0, 100);
    }
}

public record AppointmentCancellationDto(Guid AppointmentId, AppointmentStatus AppointmentStatus, Guid? RefundId, decimal? RefundAmount, RefundStatus? RefundStatus);

public enum CancelAndRefundStatus
{
    Success,
    AppointmentNotFound,

    /// <summary>Only a Scheduled appointment can be cancelled — see AppointmentStateMachine's transition table.</summary>
    NotCancellable,
}

public record CancelAndRefundResult(CancelAndRefundStatus Status, AppointmentCancellationDto? Result = null);
