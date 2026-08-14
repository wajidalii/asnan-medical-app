using Asnan.Domain.Enums;

namespace Asnan.Application.Notifications;

public record NotificationPreferenceDto(NotificationCategory Category, bool IsEnabled, bool IsDisableable);

public record SetNotificationPreferenceDto(bool IsEnabled);

public enum SetPreferenceStatus
{
    Success,

    /// <summary>Caller tried to disable a non-disableable (transactional) category — see NotificationCategoryPolicy.</summary>
    NotDisableable,
}

public record SetPreferenceResult(SetPreferenceStatus Status);
