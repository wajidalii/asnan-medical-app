using Asnan.Domain.Enums;

namespace Asnan.Application.Otps;

/// <summary>
/// Channel-agnostic facade <see cref="OtpService"/> depends on. The concrete
/// implementation dispatches to <see cref="IEmailOtpSender"/> or
/// <see cref="ISmsOtpSender"/> based on <paramref name="channel"/>.
/// </summary>
public interface IOtpSender
{
    Task SendAsync(string destination, string code, OtpChannel channel, CancellationToken cancellationToken = default);
}
