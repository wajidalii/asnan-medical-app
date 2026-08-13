using Asnan.Application.Specialties;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Backend-ready admin CRUD for specialties — no admin UI ships yet, but the
/// APIs exist per the roadmap's "backend-ready models/APIs" instruction.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/specialties")]
[Authorize(Roles = "Admin")]
public class SpecialtiesController : ControllerBase
{
    private readonly ISpecialtyService _specialtyService;

    public SpecialtiesController(ISpecialtyService specialtyService)
    {
        _specialtyService = specialtyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _specialtyService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var specialty = await _specialtyService.GetByIdAsync(id, cancellationToken);
        return specialty is null ? NotFound() : Ok(specialty);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSpecialtyDto dto, CancellationToken cancellationToken)
    {
        var result = await _specialtyService.CreateAsync(dto, cancellationToken);

        if (result.Status == SpecialtyMutationStatus.DuplicateName)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A specialty with this name already exists.");
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Specialty!.Id, version = "1.0" }, result.Specialty);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateSpecialtyDto dto, CancellationToken cancellationToken)
    {
        var result = await _specialtyService.UpdateAsync(id, dto, cancellationToken);

        return result.Status switch
        {
            SpecialtyMutationStatus.NotFound => NotFound(),
            SpecialtyMutationStatus.DuplicateName => Problem(statusCode: StatusCodes.Status409Conflict, title: "A specialty with this name already exists."),
            _ => Ok(result.Specialty),
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _specialtyService.DeleteAsync(id, cancellationToken);
        return result.Status == SpecialtyMutationStatus.NotFound ? NotFound() : NoContent();
    }
}
