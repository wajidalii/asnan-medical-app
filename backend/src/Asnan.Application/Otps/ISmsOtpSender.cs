namespace Asnan.Application.Otps;

public interface ISmsOtpSender
{
    Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
