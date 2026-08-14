using Asnan.Domain.Enums;

namespace Asnan.Application.Notifications;

public interface INotificationPreferenceService
{
    /// <summary>Every NotificationCategory value, synthesizing IsEnabled=true for any category with no opt-out row — see NotificationPreference's doc comment.</summary>
    Task<List<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SetPreferenceResult> SetPreferenceAsync(Guid userId, NotificationCategory category, bool isEnabled, CancellationToken cancellationToken = default);
}
