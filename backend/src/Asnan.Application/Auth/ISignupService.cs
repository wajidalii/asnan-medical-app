using Asnan.Application.Otps;
using Asnan.Domain.Enums;

namespace Asnan.Application.Auth;

public interface ISignupService
{
    Task<OtpRequestResult> RequestOtpAsync(string destination, OtpChannel channel, CancellationToken cancellationToken = default);

    Task<SignupVerifyOtpResult> VerifyOtpAsync(string destination, string code, OtpChannel channel, CancellationToken cancellationToken = default);

    Task<SetPasswordResult> SetPasswordAsync(string signupToken, string password, CancellationToken cancellationToken = default);
}
