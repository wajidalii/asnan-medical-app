using Asnan.Application.Otps;
using Microsoft.Extensions.Logging;

namespace Asnan.Infrastructure.Otps;

/// <summary>Dev/test-only — see <see cref="MockEmailOtpSender"/> for the registration guard.</summary>
public class MockSmsOtpSender : ISmsOtpSender
{
    private readonly ILogger<MockSmsOtpSender> _logger;

    public MockSmsOtpSender(ILogger<MockSmsOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DEV ONLY - MOCK SMS OTP] To: {PhoneNumber} Code: {Code}", phoneNumber, code);
        return Task.CompletedTask;
    }
}
