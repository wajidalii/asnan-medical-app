using Asnan.Application.Otps;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Asnan.Api.Controllers;

/// <summary>
/// Generic OTP request/verify surface, reusable across purposes (signup
/// verification, password reset, future login-2FA). Purpose-specific flows
/// (e.g. signup's "verify then issue a signup token") compose on top of this
/// rather than duplicating OTP handling — see issue #7.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/otp")]
public class OtpController : ControllerBase
{
    private readonly IOtpService _otpService;

    public OtpController(IOtpService otpService)
    {
        _otpService = otpService;
    }

    [HttpPost("request")]
    public async Task<IActionResult> RequestOtp(OtpRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _otpService.RequestAsync(dto.Destination, dto.Channel, dto.Purpose, cancellationToken);

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

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyOtp(OtpVerifyDto dto, CancellationToken cancellationToken)
    {
        var result = await _otpService.VerifyAsync(dto.Destination, dto.Code, dto.Purpose, cancellationToken);

        // Deliberately the same response for every non-success case — see
        // OtpVerifyStatus's remarks: no enumeration signal to the client.
        return result.Status switch
        {
            OtpVerifyStatus.Verified => Ok(),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired code."),
        };
    }
}
