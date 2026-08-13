using Asnan.Application.Doctors;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Backend-ready admin CRUD for doctor profiles — no admin UI ships yet, but
/// the APIs exist per the roadmap's "backend-ready models/APIs" instruction.
/// Public-facing doctor listing/search/detail endpoints are separate issues
/// (see #12, #13) and deliberately do not live in this admin-only controller.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/doctors")]
[Authorize(Roles = "Admin")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _doctorService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var doctor = await _doctorService.GetByIdAsync(id, cancellationToken);
        return doctor is null ? NotFound() : Ok(doctor);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDoctorDto dto, CancellationToken cancellationToken)
    {
        var result = await _doctorService.CreateAsync(dto, cancellationToken);

        return result.Status switch
        {
            DoctorMutationStatus.UserNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "No such user."),
            DoctorMutationStatus.ProfileAlreadyExists => Problem(statusCode: StatusCodes.Status409Conflict, title: "This user already has a doctor profile."),
            DoctorMutationStatus.SpecialtyNotFound => Problem(statusCode: StatusCodes.Status400BadRequest, title: "One or more specialties do not exist."),
            _ => CreatedAtAction(nameof(GetById), new { id = result.Doctor!.Id, version = "1.0" }, result.Doctor),
        };
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateDoctorDto dto, CancellationToken cancellationToken)
    {
        var result = await _doctorService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            DoctorMutationStatus.NotFound => NotFound(),
            DoctorMutationStatus.SpecialtyNotFound => Problem(statusCode: StatusCodes.Status400BadRequest, title: "One or more specialties do not exist."),
            _ => Ok(result.Doctor),
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _doctorService.DeleteAsync(id, cancellationToken);
        return result.Status == DoctorMutationStatus.NotFound ? NotFound() : NoContent();
    }
}
