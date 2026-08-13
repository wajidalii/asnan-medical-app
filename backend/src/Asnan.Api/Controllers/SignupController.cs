using Asnan.Application.Auth;
using Asnan.Application.Otps;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth/signup")]
public class SignupController : ControllerBase
{
    private readonly ISignupService _signupService;

    public SignupController(ISignupService signupService)
    {
        _signupService = signupService;
    }

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp(SignupRequestOtpDto dto, CancellationToken cancellationToken)
    {
        var result = await _signupService.RequestOtpAsync(dto.Destination, dto.Channel, cancellationToken);

        return result.Status switch
        {
            OtpRequestStatus.Sent => Accepted(),
            OtpRequestStatus.CooldownActive => Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Please wait before requesting another code."),
            OtpRequestStatus.RateLimited => Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many code requests. Try again later."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(OtpRequestStatus)}: {result.Status}"),
        };
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(SignupVerifyOtpDto dto, CancellationToken cancellationToken)
    {
        var result = await _signupService.VerifyOtpAsync(dto.Destination, dto.Code, dto.Channel, cancellationToken);

        if (!result.Verified)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired code.");
        }

        return Ok(new { signupToken = result.SignupToken });
    }

    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(SignupSetPasswordDto dto, CancellationToken cancellationToken)
    {
        var result = await _signupService.SetPasswordAsync(dto.SignupToken, dto.Password, cancellationToken);

        return result.Status switch
        {
            SetPasswordStatus.Success => Created(string.Empty, new { userId = result.UserId }),
            SetPasswordStatus.InvalidOrExpiredToken => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid or expired signup token."),
            SetPasswordStatus.AccountAlreadyExists => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "An account already exists for this destination. Please log in instead."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(SetPasswordStatus)}: {result.Status}"),
        };
    }
}
