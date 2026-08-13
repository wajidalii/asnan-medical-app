using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// One row per device/login. Doubles as the refresh-token "family": every
/// <see cref="RefreshToken"/> issued for this device chains off
/// <see cref="Id"/>, so revoking a family (§4.3 of ARCHITECTURE.md — reuse
/// detection, logout-this-device, logout-all-devices) means revoking this row.
/// </summary>
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Client-generated stable identifier for the installation.</summary>
    public string DeviceId { get; set; } = null!;

    public string? DeviceName { get; set; }

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Independent of any individual refresh token's sliding expiry — forces
    /// re-authentication no matter how often the app is opened.
    /// </summary>
    public DateTime AbsoluteExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
