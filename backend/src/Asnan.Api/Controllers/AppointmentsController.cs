using Asnan.Api.Extensions;
using Asnan.Application.Appointments;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Patient/doctor-facing appointment listing + self-service cancellation
/// (issue #24) — object-level authorized to the caller's own appointments.
/// Distinct from <see cref="AdminAppointmentsController"/>'s admin-only,
/// caller-specified-percentage cancel endpoint; this one always computes
/// the refund percentage server-side from the configurable cancellation
/// window (an admin caller here still bypasses the window, but gets the
/// same default-100% behavior as the dedicated admin endpoint — use that
/// one instead for a custom partial-refund override).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AppointmentListQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _appointmentService.ListAsync(User.GetUserId(), query, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, RequestCancelAppointmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _appointmentService.CancelAsync(id, User.GetUserId(), User.IsInRole("Admin"), dto, cancellationToken);

        return result.Status switch
        {
            CancelAppointmentStatus.Success => Ok(result.Result),
            CancelAppointmentStatus.AppointmentNotFound => NotFound(),
            CancelAppointmentStatus.Forbidden => Forbid(),
            CancelAppointmentStatus.NotCancellable => Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Scheduled appointment can be cancelled."),
            CancelAppointmentStatus.CancellationWindowClosed => Problem(statusCode: StatusCodes.Status409Conflict, title: "This appointment is too close to its scheduled time to cancel."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(CancelAppointmentStatus)}: {result.Status}"),
        };
    }

    /// <summary>Read-only — issue #26's cancellation flow shows this before the user confirms.</summary>
    [HttpGet("{id:guid}/cancellation-preview")]
    public async Task<IActionResult> PreviewCancellation(Guid id, CancellationToken cancellationToken)
    {
        var result = await _appointmentService.PreviewCancellationAsync(id, User.GetUserId(), User.IsInRole("Admin"), cancellationToken);

        return result.Status switch
        {
            CancelAppointmentStatus.Success => Ok(result.Preview),
            CancelAppointmentStatus.AppointmentNotFound => NotFound(),
            CancelAppointmentStatus.Forbidden => Forbid(),
            CancelAppointmentStatus.NotCancellable => Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Scheduled appointment can be cancelled."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(CancelAppointmentStatus)}: {result.Status}"),
        };
    }
}
