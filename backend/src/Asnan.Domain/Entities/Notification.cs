using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// Append-only history of dispatched notifications — issue #31. Written
/// only when a send was actually attempted via INotificationSender (i.e.
/// the recipient had at least one registered device and hadn't opted out);
/// a skipped dispatch (no devices, or an opted-out disableable category)
/// leaves no row. See NotificationDispatchService.
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public NotificationCategory Category { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string DeepLink { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }
}
