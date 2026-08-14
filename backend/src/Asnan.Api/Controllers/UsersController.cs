using Asnan.Api.Extensions;
using Asnan.Application.Profile;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// The caller's own profile + account — issue #33. Every action is scoped
/// to <c>User.GetUserId()</c>, no admin override: unlike appointments/
/// availability, there's no product requirement yet for anyone else to
/// read or edit a patient's profile.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users/me")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IPatientProfileService _profileService;

    public UsersController(IPatientProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        Ok(await _profileService.GetProfileAsync(User.GetUserId(), cancellationToken));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdatePatientProfileDto dto, CancellationToken cancellationToken) =>
        Ok(await _profileService.UpdateProfileAsync(User.GetUserId(), dto, cancellationToken));

    [HttpPost("profile/photo")]
    public async Task<IActionResult> UploadPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "A photo file is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _profileService.UploadPhotoAsync(User.GetUserId(), stream, file.Length, cancellationToken);

        return result.Status switch
        {
            PhotoProcessingStatus.Success => NoContent(),
            PhotoProcessingStatus.InvalidImage => Problem(statusCode: StatusCodes.Status400BadRequest, title: "The uploaded file is not a valid image."),
            PhotoProcessingStatus.TooLarge => Problem(statusCode: StatusCodes.Status400BadRequest, title: "The uploaded file is too large."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(PhotoProcessingStatus)}: {result.Status}"),
        };
    }

    [HttpGet("profile/photo")]
    public async Task<IActionResult> GetPhoto(CancellationToken cancellationToken)
    {
        var stream = await _profileService.GetPhotoAsync(User.GetUserId(), cancellationToken);
        return stream is null ? NotFound() : File(stream, "image/jpeg");
    }

    /// <summary>Soft-deletes the account and revokes every session — see IPatientProfileService.RequestAccountDeletionAsync's doc comment.</summary>
    [HttpDelete]
    public async Task<IActionResult> RequestAccountDeletion(CancellationToken cancellationToken)
    {
        await _profileService.RequestAccountDeletionAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
