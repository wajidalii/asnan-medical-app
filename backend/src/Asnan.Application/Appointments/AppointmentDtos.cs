using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Appointments;

public enum AppointmentListScope
{
    Upcoming,
    Past,
}

public record AppointmentListQuery(AppointmentListScope Scope = AppointmentListScope.Upcoming, int Page = 1, int PageSize = 20);

/// <summary>
/// <see cref="ChatConversationId"/> is null until the appointment has
/// reached (or passed through) Scheduled — a ChatConversation is only ever
/// created at that point (#20) — and is the entry point the mobile chat
/// feature (#29) navigates into from the appointment details screen.
/// </summary>
public record AppointmentSummaryDto(
    Guid Id,
    Guid DoctorProfileId,
    string DoctorFullName,
    DateTime SlotStartUtc,
    DateTime SlotEndUtc,
    AppointmentStatus Status,
    decimal ConsultationFee,
    string Currency,
    Guid? ChatConversationId);

/// <summary>
/// No refund-percentage field, deliberately: unlike the admin endpoint's
/// <see cref="Payments.CancelAppointmentDto"/>, the percentage here is
/// always server-computed from <see cref="CancellationPolicyOptions"/> —
/// never client-supplied, since that's the amount of real money refunded.
/// </summary>
public record RequestCancelAppointmentDto(string? Reason);

public class RequestCancelAppointmentDtoValidator : AbstractValidator<RequestCancelAppointmentDto>
{
    public RequestCancelAppointmentDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public enum CancelAppointmentStatus
{
    Success,
    AppointmentNotFound,

    /// <summary>Caller is neither the appointment's patient, its doctor, nor an admin.</summary>
    Forbidden,

    /// <summary>Only a Scheduled appointment can be cancelled.</summary>
    NotCancellable,

    /// <summary>Too close to the appointment for any configured refund tier to apply.</summary>
    CancellationWindowClosed,
}

public record CancelAppointmentResult(CancelAppointmentStatus Status, Payments.AppointmentCancellationDto? Result = null);

/// <summary>
/// Read-only preview of what cancelling right now would do — mobile (#26)
/// shows this before the user confirms, per "Cancellation flow shows the
/// applicable refund policy before confirming". <see cref="IsAllowed"/>
/// false is a normal, non-error preview outcome (the window has closed),
/// distinct from <see cref="CancelAppointmentStatus.CancellationWindowClosed"/>
/// which is what the actual (mutating) cancel action returns if attempted anyway.
/// </summary>
public record CancellationPreviewDto(Guid AppointmentId, bool IsAllowed, int RefundPercentage, decimal RefundAmount, string Currency);

public record PreviewCancellationResult(CancelAppointmentStatus Status, CancellationPreviewDto? Preview = null);

public enum GetAppointmentStatus
{
    Success,
    AppointmentNotFound,

    /// <summary>Caller is neither the appointment's patient, its doctor, nor an admin.</summary>
    Forbidden,
}

public record GetAppointmentResult(GetAppointmentStatus Status, AppointmentSummaryDto? Appointment = null);
