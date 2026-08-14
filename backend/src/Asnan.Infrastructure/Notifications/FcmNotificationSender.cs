using Asnan.Application.Notifications;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Asnan.Infrastructure.Notifications;

/// <summary>
/// Real FCM delivery via the FirebaseAdmin SDK — issue #30. Only
/// constructed when Notification:Provider is explicitly "Fcm" and
/// credentials are configured (see DependencyInjection.AddNotificationSender).
/// </summary>
public class FcmNotificationSender : INotificationSender
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FcmNotificationSender> _logger;

    public FcmNotificationSender(FirebaseMessaging messaging, ILogger<FcmNotificationSender> logger)
    {
        _messaging = messaging;
        _logger = logger;
    }

    public async Task SendAsync(IReadOnlyList<string> fcmTokens, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (fcmTokens.Count == 0) return;

        // MulticastMessage.Tokens is obsolete in favor of .Fids (Firebase
        // Installation IDs) — a different identifier the mobile client (#32)
        // never produces. NotificationDevice.FcmToken is a standard FCM
        // registration token from FirebaseMessaging.instance.getToken(), so
        // .Tokens is the correct property here, not a lagging migration.
#pragma warning disable CS0618
        var message = new MulticastMessage
        {
            Tokens = fcmTokens,
            Notification = new Notification { Title = notification.Title, Body = notification.Body },
            Data = new Dictionary<string, string> { ["deepLink"] = notification.DeepLink },
        };
#pragma warning restore CS0618

        var response = await _messaging.SendEachForMulticastAsync(message, cancellationToken);

        if (response.FailureCount == 0) return;

        // Per-token failures (expired/uninstalled tokens etc.) are expected
        // and must not fail the whole batch — cleaning up permanently-invalid
        // tokens from NotificationDevices is a documented follow-up, not done here.
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var single = response.Responses[i];
            if (!single.IsSuccess)
            {
                _logger.LogWarning(single.Exception, "FCM send failed for token index {Index}.", i);
            }
        }
    }
}
