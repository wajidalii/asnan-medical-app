using Asnan.Domain.Enums;

namespace Asnan.Application.Notifications;

/// <summary>
/// Device registration only (issue #30) — no push-preference or delivery
/// concerns here, see INotificationSender/INotificationDispatchService for
/// those.
/// </summary>
public interface INotificationDeviceService
{
    /// <summary>Upserts by token — see NotificationDevice's doc comment for the reassignment behavior.</summary>
    Task RegisterAsync(Guid userId, string fcmToken, DevicePlatform platform, CancellationToken cancellationToken = default);

    /// <summary>No-ops if the token isn't currently registered to this user (already removed, or reassigned to someone else) — removal is intentionally idempotent.</summary>
    Task RemoveAsync(Guid userId, string fcmToken, CancellationToken cancellationToken = default);
}
