using Asnan.Domain.Enums;

namespace Asnan.Domain.Common;

/// <summary>
/// Which <see cref="NotificationCategory"/> values a user may opt out of —
/// ARCHITECTURE.md §10: "transactional notifications tied to
/// security/payment are not user-disable-able." Shared by
/// NotificationPreferenceService (rejects a disable of a non-disableable
/// category) and the Flutter preferences screen's read-only rendering.
/// </summary>
public static class NotificationCategoryPolicy
{
    private static readonly HashSet<NotificationCategory> NonDisableable =
        [NotificationCategory.AppointmentUpdates, NotificationCategory.PaymentUpdates];

    public static bool IsDisableable(NotificationCategory category) => !NonDisableable.Contains(category);
}
