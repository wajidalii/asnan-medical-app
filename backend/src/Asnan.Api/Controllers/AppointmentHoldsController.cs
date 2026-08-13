using Asnan.Api.Extensions;
using Asnan.Application.Availability;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Short-TTL slot claims — ARCHITECTURE.md §6's booking critical section.
/// Requires authentication (a hold belongs to whichever user made it); not
/// role-restricted since booking isn't Patient-exclusive at the API level.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/appointments/holds")]
[Authorize]
public class AppointmentHoldsController : ControllerBase
{
    private readonly IAppointmentHoldService _holdService;

    public AppointmentHoldsController(IAppointmentHoldService holdService)
    {
        _holdService = holdService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateHoldDto dto, CancellationToken cancellationToken)
    {
        var result = await _holdService.CreateAsync(User.GetUserId(), dto, cancellationToken);

        return result.Status switch
        {
            CreateHoldStatus.DoctorNotFound => NotFound(),
            CreateHoldStatus.SlotNotAvailable => Problem(statusCode: StatusCodes.Status409Conflict, title: "This slot is no longer available."),
            CreateHoldStatus.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "This slot is no longer available."),
            _ => StatusCode(StatusCodes.Status201Created, result.Hold),
        };
    }
}
