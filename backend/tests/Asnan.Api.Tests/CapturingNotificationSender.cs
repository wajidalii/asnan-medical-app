using Asnan.Application.Notifications;

namespace Asnan.Api.Tests;

/// <summary>
/// The captured-call test double referenced by issue #31's testing
/// requirement — swapped in for the real INotificationSender via
/// WebApplicationFactory.WithWebHostBuilder so trigger-wiring tests can
/// assert "exactly one send" without touching a real push provider.
/// </summary>
public class CapturingNotificationSender : INotificationSender
{
    private readonly List<(IReadOnlyList<string> Tokens, PushNotification Notification)> _calls = [];

    public IReadOnlyList<(IReadOnlyList<string> Tokens, PushNotification Notification)> Calls
    {
        get
        {
            lock (_calls)
            {
                return _calls.ToList();
            }
        }
    }

    public Task SendAsync(IReadOnlyList<string> fcmTokens, PushNotification notification, CancellationToken cancellationToken = default)
    {
        lock (_calls)
        {
            _calls.Add((fcmTokens, notification));
        }

        return Task.CompletedTask;
    }
}
