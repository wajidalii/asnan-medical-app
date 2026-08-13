namespace Asnan.Application.Auth;

public interface IRefreshTokenService
{
    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes only the session tied to this specific refresh token — "logout this device".</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session for the user — "logout everywhere".</summary>
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionSummary>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
