using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// One registered push-capable device — issue #30. Deduped by
/// <see cref="FcmToken"/> rather than by user: a token can only ever belong
/// to one row, so re-registering the same physical device under a
/// different account (reinstall + different login) reassigns
/// <see cref="UserId"/> on the existing row instead of creating a
/// duplicate — see NotificationDeviceService.RegisterAsync.
/// </summary>
public class NotificationDevice : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string FcmToken { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
