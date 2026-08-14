using Asnan.Domain.Enums;

namespace Asnan.Application.Notifications;

/// <summary>
/// The layer between a domain event and raw push dispatch (issue #31) —
/// resolves the recipient's devices, applies NotificationPreferences
/// opt-outs (skipped entirely for non-disableable categories), and calls
/// INotificationSender. Every trigger call site (payments, appointments,
/// reminders, chat, availability) goes through this rather than
/// INotificationSender directly.
/// </summary>
public interface INotificationDispatchService
{
    /// <summary>No-ops silently (no send, no history row) if the recipient has opted out of a disableable category, or has no registered devices.</summary>
    Task DispatchAsync(Guid userId, NotificationCategory category, PushNotification notification, CancellationToken cancellationToken = default);
}
