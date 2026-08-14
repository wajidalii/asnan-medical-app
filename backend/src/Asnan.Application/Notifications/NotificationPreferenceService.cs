using Asnan.Application.Common;
using Asnan.Domain.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Notifications;

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly IApplicationDbContext _db;

    public NotificationPreferenceService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var disabledCategories = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .Select(p => p.Category)
            .ToListAsync(cancellationToken);
        var disabledSet = disabledCategories.ToHashSet();

        return Enum.GetValues<NotificationCategory>()
            .Select(category => new NotificationPreferenceDto(
                category,
                IsEnabled: !disabledSet.Contains(category),
                IsDisableable: NotificationCategoryPolicy.IsDisableable(category)))
            .ToList();
    }

    public async Task<SetPreferenceResult> SetPreferenceAsync(Guid userId, NotificationCategory category, bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (!isEnabled && !NotificationCategoryPolicy.IsDisableable(category))
        {
            return new SetPreferenceResult(SetPreferenceStatus.NotDisableable);
        }

        var existing = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Category == category, cancellationToken);

        if (isEnabled)
        {
            if (existing is not null)
            {
                _db.NotificationPreferences.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        else if (existing is null)
        {
            _db.NotificationPreferences.Add(new NotificationPreference { UserId = userId, Category = category });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new SetPreferenceResult(SetPreferenceStatus.Success);
    }
}
