using Asnan.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Asnan.Infrastructure.Notifications;

/// <summary>
/// Logs instead of pushing — selected whenever Notification:Provider isn't
/// explicitly "Fcm", including in Production, until real Firebase
/// credentials exist (external-config follow-up referenced in issue #30).
/// Same "no safety implication to logging instead of pushing" rationale as
/// LoggingReminderSender/LoggingOfflineMessageNotifier, so — unlike the
/// OTP/Payment mocks — this is not Development-gated.
/// </summary>
public class NoOpNotificationSender : INotificationSender
{
    private readonly ILogger<NoOpNotificationSender> _logger;

    public NoOpNotificationSender(ILogger<NoOpNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(IReadOnlyList<string> fcmTokens, PushNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Push notification {Title} -> {DeviceCount} device(s) — no real push provider configured (Notification:Provider != \"Fcm\").",
            notification.Title,
            fcmTokens.Count);
        return Task.CompletedTask;
    }
}
