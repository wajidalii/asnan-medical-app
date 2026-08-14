using Asnan.Application.Common;
using Asnan.Domain.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Notifications;

public class NotificationDispatchService : INotificationDispatchService
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationSender _sender;

    public NotificationDispatchService(IApplicationDbContext db, INotificationSender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task DispatchAsync(Guid userId, NotificationCategory category, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (NotificationCategoryPolicy.IsDisableable(category))
        {
            var isOptedOut = await _db.NotificationPreferences
                .AnyAsync(p => p.UserId == userId && p.Category == category, cancellationToken);
            if (isOptedOut) return;
        }

        var tokens = await _db.NotificationDevices
            .Where(d => d.UserId == userId)
            .Select(d => d.FcmToken)
            .ToListAsync(cancellationToken);
        if (tokens.Count == 0) return;

        await _sender.SendAsync(tokens, notification, cancellationToken);

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Category = category,
            Title = notification.Title,
            Body = notification.Body,
            DeepLink = notification.DeepLink,
            SentAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
