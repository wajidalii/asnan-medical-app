using Asnan.Api.Extensions;
using Asnan.Application.Notifications;
using Asnan.Domain.Enums;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Device registration + per-category preferences (issue #30). Registration
/// is client-driven: the FCM token only exists once the Firebase SDK has
/// produced one, which happens after login/app-start on the client, not
/// during the login request itself — so the mobile app calls POST here
/// once it has a token, and DELETE before completing its own logout flow
/// (issue #32), rather than this being wired inside AuthController.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationDeviceService _deviceService;
    private readonly INotificationPreferenceService _preferenceService;

    public NotificationsController(INotificationDeviceService deviceService, INotificationPreferenceService preferenceService)
    {
        _deviceService = deviceService;
        _preferenceService = preferenceService;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(RegisterDeviceDto dto, CancellationToken cancellationToken)
    {
        await _deviceService.RegisterAsync(User.GetUserId(), dto.FcmToken, dto.Platform, cancellationToken);
        return NoContent();
    }

    [HttpDelete("devices")]
    public async Task<IActionResult> RemoveDevice(RemoveDeviceDto dto, CancellationToken cancellationToken)
    {
        await _deviceService.RemoveAsync(User.GetUserId(), dto.FcmToken, cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        var preferences = await _preferenceService.GetPreferencesAsync(User.GetUserId(), cancellationToken);
        return Ok(preferences);
    }

    [HttpPut("preferences/{category}")]
    public async Task<IActionResult> SetPreference(NotificationCategory category, SetNotificationPreferenceDto dto, CancellationToken cancellationToken)
    {
        var result = await _preferenceService.SetPreferenceAsync(User.GetUserId(), category, dto.IsEnabled, cancellationToken);

        return result.Status switch
        {
            SetPreferenceStatus.Success => NoContent(),
            SetPreferenceStatus.NotDisableable => Problem(statusCode: StatusCodes.Status400BadRequest, title: "This notification category cannot be disabled."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SetPreferenceStatus)}: {result.Status}"),
        };
    }
}
