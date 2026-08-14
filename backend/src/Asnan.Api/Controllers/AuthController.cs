using Asnan.Api.Extensions;
using Asnan.Application.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asnan.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ILoginService _loginService;
    private readonly IRefreshTokenService _refreshTokenService;

    public AuthController(ILoginService loginService, IRefreshTokenService refreshTokenService)
    {
        _loginService = loginService;
        _refreshTokenService = refreshTokenService;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await _loginService.LoginAsync(dto.Identifier, dto.Password, dto.DeviceId, dto.DeviceName, cancellationToken);

        if (result.Status == LoginStatus.InvalidCredentials)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.");
        }

        return Ok(new
        {
            accessToken = result.AccessToken,
            accessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            refreshToken = result.RefreshToken,
            refreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc,
        });
    }

    /// <summary>
    /// Called on app open/cold-start (silent refresh) as well as reactively on a
    /// 401 mid-session — see ARCHITECTURE.md §4.5. No [Authorize]: the access
    /// token may already be expired, which is exactly why this is being called.
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshDto dto, CancellationToken cancellationToken)
    {
        var result = await _refreshTokenService.RefreshAsync(dto.RefreshToken, cancellationToken);

        if (result.Status != RefreshStatus.Success)
        {
            // Deliberately the same generic response for InvalidToken, SessionRevoked,
            // and ReuseDetected — the client's only actionable response in every case
            // is "route to login," and the specific reason isn't the caller's business.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid or expired session. Please log in again.");
        }

        return Ok(new
        {
            accessToken = result.AccessToken,
            accessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            refreshToken = result.RefreshToken,
            refreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc,
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutDto dto, CancellationToken cancellationToken)
    {
        await _refreshTokenService.LogoutAsync(dto.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _refreshTokenService.LogoutAllAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var sessions = await _refreshTokenService.GetActiveSessionsAsync(User.GetUserId(), cancellationToken);
        return Ok(sessions);
    }

    /// <summary>Issue #35 — revokes one specific device's session, unlike POST /logout (the caller's own current session only) or /logout-all (every session).</summary>
    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken)
    {
        var revoked = await _refreshTokenService.RevokeSessionAsync(User.GetUserId(), id, cancellationToken);
        return revoked ? NoContent() : NotFound();
    }
}
