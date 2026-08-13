using Asnan.Api.Extensions;
using Asnan.Application.Availability;
using Asnan.Application.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Date-specific schedule override CRUD — authorized to the owning doctor or
/// an Admin, same rationale as <see cref="DoctorSchedulesController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/{doctorId:guid}/availability-exceptions")]
[Authorize]
public class DoctorAvailabilityExceptionsController : ControllerBase
{
    private readonly IAvailabilityExceptionService _exceptionService;

    public DoctorAvailabilityExceptionsController(IAvailabilityExceptionService exceptionService)
    {
        _exceptionService = exceptionService;
    }

    private CallerContext Caller => new(User.GetUserId(), User.IsInRole("Admin"));

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid doctorId, CancellationToken cancellationToken)
    {
        var result = await _exceptionService.GetAllAsync(doctorId, Caller, cancellationToken);

        return result.Status switch
        {
            AvailabilityExceptionQueryStatus.DoctorNotFound => NotFound(),
            AvailabilityExceptionQueryStatus.Forbidden => Forbid(),
            _ => Ok(result.Exceptions),
        };
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid doctorId, CreateAvailabilityExceptionDto dto, CancellationToken cancellationToken)
    {
        var result = await _exceptionService.CreateAsync(doctorId, dto, Caller, cancellationToken);

        return result.Status switch
        {
            AvailabilityExceptionMutationStatus.DoctorNotFound => NotFound(),
            AvailabilityExceptionMutationStatus.Forbidden => Forbid(),
            AvailabilityExceptionMutationStatus.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "This conflicts with an existing exception on that date."),
            _ => CreatedAtAction(nameof(GetAll), new { doctorId, version = "1.0" }, result.Exception),
        };
    }

    [HttpPut("{exceptionId:guid}")]
    public async Task<IActionResult> Update(Guid doctorId, Guid exceptionId, UpdateAvailabilityExceptionDto dto, CancellationToken cancellationToken)
    {
        var result = await _exceptionService.UpdateAsync(doctorId, exceptionId, dto, Caller, cancellationToken);

        return result.Status switch
        {
            AvailabilityExceptionMutationStatus.DoctorNotFound => NotFound(),
            AvailabilityExceptionMutationStatus.Forbidden => Forbid(),
            AvailabilityExceptionMutationStatus.NotFound => NotFound(),
            AvailabilityExceptionMutationStatus.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "This conflicts with an existing exception on that date."),
            _ => Ok(result.Exception),
        };
    }

    [HttpDelete("{exceptionId:guid}")]
    public async Task<IActionResult> Delete(Guid doctorId, Guid exceptionId, CancellationToken cancellationToken)
    {
        var result = await _exceptionService.DeleteAsync(doctorId, exceptionId, Caller, cancellationToken);

        return result.Status switch
        {
            AvailabilityExceptionMutationStatus.DoctorNotFound => NotFound(),
            AvailabilityExceptionMutationStatus.Forbidden => Forbid(),
            AvailabilityExceptionMutationStatus.NotFound => NotFound(),
            _ => NoContent(),
        };
    }
}
