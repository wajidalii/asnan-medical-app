using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Notifications;

public class NotificationDeviceService : INotificationDeviceService
{
    private readonly IApplicationDbContext _db;

    public NotificationDeviceService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RegisterAsync(Guid userId, string fcmToken, DevicePlatform platform, CancellationToken cancellationToken = default)
    {
        var existing = await _db.NotificationDevices.FirstOrDefaultAsync(d => d.FcmToken == fcmToken, cancellationToken);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            _db.NotificationDevices.Add(new NotificationDevice
            {
                UserId = userId,
                FcmToken = fcmToken,
                Platform = platform,
                LastSeenAtUtc = now,
            });
        }
        else
        {
            // Same token, possibly a different account (reinstall + different
            // login) — reassign rather than reject, see NotificationDevice's doc comment.
            existing.UserId = userId;
            existing.Platform = platform;
            existing.LastSeenAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid userId, string fcmToken, CancellationToken cancellationToken = default)
    {
        // Scoped to the caller's own userId — a token that's since been
        // reassigned to someone else must not be removable by the caller.
        var existing = await _db.NotificationDevices
            .FirstOrDefaultAsync(d => d.FcmToken == fcmToken && d.UserId == userId, cancellationToken);

        if (existing is null) return;

        _db.NotificationDevices.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
