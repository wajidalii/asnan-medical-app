using Asnan.Application.Otps;
using Asnan.Domain.Enums;

namespace Asnan.Infrastructure.Otps;

public class CompositeOtpSender : IOtpSender
{
    private readonly IEmailOtpSender _email;
    private readonly ISmsOtpSender _sms;

    public CompositeOtpSender(IEmailOtpSender email, ISmsOtpSender sms)
    {
        _email = email;
        _sms = sms;
    }

    public Task SendAsync(string destination, string code, OtpChannel channel, CancellationToken cancellationToken = default)
    {
        return channel switch
        {
            OtpChannel.Email => _email.SendAsync(destination, code, cancellationToken),
            OtpChannel.Sms => _sms.SendAsync(destination, code, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown OTP channel."),
        };
    }
}
