using Asnan.Api.Extensions;
using Asnan.Application.Availability;
using Asnan.Application.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Recurring weekly schedule-template CRUD — authorized to the owning doctor
/// or an Admin (object-level authorization, ARCHITECTURE.md §2.2), not a
/// public endpoint. Patient-facing computed availability is a separate API.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/{doctorId:guid}/schedules")]
[Authorize]
public class DoctorSchedulesController : ControllerBase
{
    private readonly IDoctorScheduleService _scheduleService;

    public DoctorSchedulesController(IDoctorScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    private CallerContext Caller => new(User.GetUserId(), User.IsInRole("Admin"));

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid doctorId, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.GetAllAsync(doctorId, Caller, cancellationToken);

        return result.Status switch
        {
            ScheduleQueryStatus.DoctorNotFound => NotFound(),
            ScheduleQueryStatus.Forbidden => Forbid(),
            _ => Ok(result.Schedules),
        };
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid doctorId, CreateDoctorScheduleDto dto, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.CreateAsync(doctorId, dto, Caller, cancellationToken);

        return result.Status switch
        {
            ScheduleMutationStatus.DoctorNotFound => NotFound(),
            ScheduleMutationStatus.Forbidden => Forbid(),
            ScheduleMutationStatus.OverlappingWindow => Problem(statusCode: StatusCodes.Status409Conflict, title: "This window overlaps an existing schedule for that day."),
            _ => CreatedAtAction(nameof(GetAll), new { doctorId, version = "1.0" }, result.Schedule),
        };
    }

    [HttpPut("{scheduleId:guid}")]
    public async Task<IActionResult> Update(Guid doctorId, Guid scheduleId, UpdateDoctorScheduleDto dto, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.UpdateAsync(doctorId, scheduleId, dto, Caller, cancellationToken);

        return result.Status switch
        {
            ScheduleMutationStatus.DoctorNotFound => NotFound(),
            ScheduleMutationStatus.Forbidden => Forbid(),
            ScheduleMutationStatus.NotFound => NotFound(),
            ScheduleMutationStatus.OverlappingWindow => Problem(statusCode: StatusCodes.Status409Conflict, title: "This window overlaps an existing schedule for that day."),
            _ => Ok(result.Schedule),
        };
    }

    [HttpDelete("{scheduleId:guid}")]
    public async Task<IActionResult> Delete(Guid doctorId, Guid scheduleId, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.DeleteAsync(doctorId, scheduleId, Caller, cancellationToken);

        return result.Status switch
        {
            ScheduleMutationStatus.DoctorNotFound => NotFound(),
            ScheduleMutationStatus.Forbidden => Forbid(),
            ScheduleMutationStatus.NotFound => NotFound(),
            _ => NoContent(),
        };
    }
}
