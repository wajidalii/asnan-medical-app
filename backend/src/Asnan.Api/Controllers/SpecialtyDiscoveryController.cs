using Asnan.Application.Specialties;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Public read-only specialty list — needed for client-side filter UIs (e.g.
/// the Flutter doctor-discovery specialty filter, #14). No auth required,
/// same rationale as <see cref="DoctorDiscoveryController"/>. Distinct from
/// the admin-only management API in <see cref="SpecialtiesController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/specialties")]
public class SpecialtyDiscoveryController : ControllerBase
{
    private readonly ISpecialtyService _specialtyService;

    public SpecialtyDiscoveryController(ISpecialtyService specialtyService)
    {
        _specialtyService = specialtyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _specialtyService.GetAllAsync(cancellationToken));
    }
}
