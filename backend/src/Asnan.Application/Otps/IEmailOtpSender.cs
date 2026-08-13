namespace Asnan.Application.Otps;

/// <summary>Swappable independently of <see cref="ISmsOtpSender"/> — a real email provider
/// can be wired in without touching SMS, and vice versa.</summary>
public interface IEmailOtpSender
{
    Task SendAsync(string emailAddress, string code, CancellationToken cancellationToken = default);
}
