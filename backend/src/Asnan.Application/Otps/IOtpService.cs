using Asnan.Domain.Enums;

namespace Asnan.Application.Otps;

public interface IOtpService
{
    Task<OtpRequestResult> RequestAsync(
        string destination,
        OtpChannel channel,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    Task<OtpVerifyResult> VerifyAsync(
        string destination,
        string code,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);
}
