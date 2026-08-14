using Asnan.Api.Extensions;
using Asnan.Application.Payments;
using Asnan.Domain.Enums;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Admin-triggered cancel-and-refund (issue #21). Deliberately separate
/// from the future patient/doctor self-service cancellation endpoint
/// (Milestone 6, windowed cancellation policy) — this is the
/// system/admin-side mechanism that endpoint will eventually call into with
/// a policy-computed refund percentage.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/appointments")]
[Authorize(Roles = "Admin")]
public class AdminAppointmentsController : ControllerBase
{
    private readonly IRefundService _refundService;

    public AdminAppointmentsController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelAppointmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _refundService.CancelAndRefundAsync(id, AppointmentStatus.CancelledByAdmin, User.GetUserId(), dto, cancellationToken);

        return result.Status switch
        {
            CancelAndRefundStatus.Success => Ok(result.Result),
            CancelAndRefundStatus.AppointmentNotFound => NotFound(),
            CancelAndRefundStatus.NotCancellable => Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Scheduled appointment can be cancelled."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(CancelAndRefundStatus)}: {result.Status}"),
        };
    }
}
