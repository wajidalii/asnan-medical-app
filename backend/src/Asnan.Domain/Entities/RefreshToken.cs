using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Single-use. Rotated on every refresh (§4.3): using a token invalidates it
/// and issues a new one in the same <see cref="UserSession"/> family. Reuse
/// of an already-<see cref="UsedAtUtc"/> token is the reuse-detection signal
/// that revokes the whole family.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserSessionId { get; set; }

    public UserSession UserSession { get; set; } = null!;

    /// <summary>Never store the raw token — only its hash.</summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>Sliding expiry: extended by issuing a new token in the same family on use.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }
}
