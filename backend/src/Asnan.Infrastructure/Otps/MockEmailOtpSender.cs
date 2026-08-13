using Asnan.Application.Otps;
using Microsoft.Extensions.Logging;

namespace Asnan.Infrastructure.Otps;

/// <summary>
/// Dev/test-only. Registration is refused outside Development — see
/// DependencyInjection.AddInfrastructure — so this can never end up logging
/// a real user's OTP code in a deployed environment.
/// </summary>
public class MockEmailOtpSender : IEmailOtpSender
{
    private readonly ILogger<MockEmailOtpSender> _logger;

    public MockEmailOtpSender(ILogger<MockEmailOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string emailAddress, string code, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DEV ONLY - MOCK EMAIL OTP] To: {EmailAddress} Code: {Code}", emailAddress, code);
        return Task.CompletedTask;
    }
}
