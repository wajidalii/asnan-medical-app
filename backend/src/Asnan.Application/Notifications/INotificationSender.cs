namespace Asnan.Application.Notifications;

/// <summary>Deliberately narrow: title/body/deepLink only, no medical detail — ARCHITECTURE.md §10. Content policy is enforced by callers (issue #31), not this type.</summary>
public record PushNotification(string Title, string Body, string DeepLink);

/// <summary>
/// Raw push dispatch — issue #30. No device lookup and no
/// NotificationPreferences filtering here; those live one layer up (issue
/// #31's dispatch service), so this stays swappable the same way
/// IOtpSender/IPaymentProvider are: NoOpNotificationSender until Firebase
/// credentials exist, then FcmNotificationSender (see
/// Infrastructure.DependencyInjection.AddNotificationSender).
/// </summary>
public interface INotificationSender
{
    /// <summary>Best-effort per token — one invalid/expired token must not fail the whole batch.</summary>
    Task SendAsync(IReadOnlyList<string> fcmTokens, PushNotification notification, CancellationToken cancellationToken = default);
}
