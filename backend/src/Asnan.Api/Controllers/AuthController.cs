using Asnan.Application.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asnan.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ILoginService _loginService;

    public AuthController(ILoginService loginService)
    {
        _loginService = loginService;
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
}
