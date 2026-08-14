using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Appointments;

public enum AppointmentListScope
{
    Upcoming,
    Past,
}

public record AppointmentListQuery(AppointmentListScope Scope = AppointmentListScope.Upcoming, int Page = 1, int PageSize = 20);

public record AppointmentSummaryDto(
    Guid Id,
    Guid DoctorProfileId,
    string DoctorFullName,
    DateTime SlotStartUtc,
    DateTime SlotEndUtc,
    AppointmentStatus Status,
    decimal ConsultationFee,
    string Currency);

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
