namespace Asnan.Application.Auth;

public enum RefreshStatus
{
    Success,

    /// <summary>Unknown/never-existed token — generic, same client-facing
    /// message as every other failure case here.</summary>
    InvalidToken,

    /// <summary>Token exists but its session was already revoked (logout,
    /// prior reuse-detection, or absolute expiry).</summary>
    SessionRevoked,

    /// <summary>
    /// This token was already used (rotated) once before — the standard
    /// signal of a stolen token being replayed. The whole session is revoked
    /// as a side effect of this result, not something the caller triggers
    /// separately.
    /// </summary>
    ReuseDetected,
}

public record RefreshResult(
    RefreshStatus Status,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAtUtc = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAtUtc = null);

public record SessionSummary(
    Guid SessionId,
    string DeviceId,
    string? DeviceName,
    DateTime LastSeenAtUtc,
    DateTime AbsoluteExpiresAtUtc);
