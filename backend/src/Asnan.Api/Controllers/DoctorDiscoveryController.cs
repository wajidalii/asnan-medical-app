using Asnan.Application.Doctors;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Public patient-facing doctor directory — no auth required, browsing
/// doctors is not a gated action. Distinct from the admin-only management
/// API in <see cref="DoctorsController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors")]
public class DoctorDiscoveryController : ControllerBase
{
    private readonly IDoctorSearchService _searchService;

    public DoctorDiscoveryController(IDoctorSearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] DoctorSearchQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _searchService.SearchAsync(query, cancellationToken));
    }
}
