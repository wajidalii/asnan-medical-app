using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// An explicit per-user opt-out of one <see cref="NotificationCategory"/> —
/// issue #30. The presence of a row means "disabled"; the absence of a row
/// for a given (user, category) means the default of enabled — so a newly
/// added category is enabled for every existing user with no migration
/// backfill needed. Only categories where
/// <see cref="NotificationCategoryPolicy.IsDisableable"/> is true may ever
/// have a row here — enforced by NotificationPreferenceService, not by the
/// database.
/// </summary>
public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public NotificationCategory Category { get; set; }
}
