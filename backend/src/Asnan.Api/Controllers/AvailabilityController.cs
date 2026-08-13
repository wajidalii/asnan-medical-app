using Asnan.Application.Availability;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Computed availability — public, no auth required (patients need this
/// before/without a booking flow started). ARCHITECTURE.md §6: slots are
/// computed on read here, never pre-materialized.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/availability/doctors/{doctorId:guid}")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityComputationService _availabilityService;

    public AvailabilityController(IAvailabilityComputationService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailability(Guid doctorId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _availabilityService.GetAvailabilityAsync(doctorId, date, cancellationToken);

        return result.Status == DoctorAvailabilityStatus.DoctorNotFound ? NotFound() : Ok(result.Availability);
    }
}
