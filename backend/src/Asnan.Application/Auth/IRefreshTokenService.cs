namespace Asnan.Application.Auth;

public interface IRefreshTokenService
{
    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes only the session tied to this specific refresh token — "logout this device".</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session for the user — "logout everywhere".</summary>
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionSummary>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes one specific session by id — issue #35's "log out this
    /// [other] device," which LogoutAsync can't do since it only ever
    /// revokes the session tied to the refresh token in hand, never an
    /// arbitrary one by id. Object-level authorized: only revokes a session
    /// that actually belongs to <paramref name="userId"/>. Returns false if
    /// no such active session exists for that user (already revoked, or
    /// never existed) — the caller maps that to 404, not an error.
    /// </summary>
    Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
