using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Application.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _options;

    public RefreshTokenService(IApplicationDbContext db, IJwtTokenService jwtTokenService, IOptions<JwtOptions> options)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _options = options.Value;
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokenHash = OpaqueTokenGenerator.Hash(refreshToken);

        var token = await _db.RefreshTokens
            .Include(t => t.UserSession).ThenInclude(s => s.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            return new RefreshResult(RefreshStatus.InvalidToken);
        }

        var session = token.UserSession;

        if (session.RevokedAtUtc is not null)
        {
            return new RefreshResult(RefreshStatus.SessionRevoked);
        }

        if (session.AbsoluteExpiresAtUtc <= now)
        {
            session.RevokedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new RefreshResult(RefreshStatus.SessionRevoked);
        }

        if (token.UsedAtUtc is not null || token.ExpiresAtUtc <= now)
        {
            // Either an explicitly-already-rotated token being replayed, or one
            // that simply expired unused — either way this token cannot be
            // trusted again, and a replay of an already-rotated token is exactly
            // the theft signal §4.3 calls out: revoke the whole family.
            session.RevokedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new RefreshResult(RefreshStatus.ReuseDetected);
        }

        token.UsedAtUtc = now;
        session.LastSeenAtUtc = now;

        var rawNewRefreshToken = OpaqueTokenGenerator.Generate();
        var newRefreshTokenExpiresAtUtc = now.AddDays(_options.RefreshTokenSlidingExpiryDays);
        var newRefreshTokenEntity = new RefreshToken
        {
            UserSessionId = session.Id,
            TokenHash = OpaqueTokenGenerator.Hash(rawNewRefreshToken),
            ExpiresAtUtc = newRefreshTokenExpiresAtUtc,
        };
        _db.RefreshTokens.Add(newRefreshTokenEntity);

        var roles = session.User.UserRoles.Select(ur => ur.Role.Name);
        var (accessToken, accessTokenExpiresAtUtc) = _jwtTokenService.GenerateAccessToken(session.UserId, roles);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost a race with a concurrent refresh call using the same source
            // token — see RefreshTokenConfiguration's IsConcurrencyToken comment.
            // Whoever wins gets a valid rotation; the loser is treated exactly
            // like an explicit reuse, since from its perspective this token was
            // just used by someone else.
            //
            // Discard this attempt's now-poisoned changes (the conflicting token
            // update and the new token we were about to mint) before retrying —
            // otherwise the retry would immediately fail the same way.
            _db.RefreshTokens.Remove(newRefreshTokenEntity);
            await _db.Entry(token).ReloadAsync(cancellationToken);

            session.RevokedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new RefreshResult(RefreshStatus.ReuseDetected);
        }

        return new RefreshResult(
            RefreshStatus.Success,
            accessToken,
            accessTokenExpiresAtUtc,
            rawNewRefreshToken,
            newRefreshTokenExpiresAtUtc);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = OpaqueTokenGenerator.Hash(refreshToken);

        var token = await _db.RefreshTokens
            .Include(t => t.UserSession)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        // Unknown token: logout is idempotent from the caller's point of view —
        // there's nothing to revoke, which is not an error condition.
        if (token is null || token.UserSession.RevokedAtUtc is not null)
        {
            return;
        }

        token.UserSession.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.AbsoluteExpiresAtUtc > now)
            .OrderByDescending(s => s.LastSeenAtUtc)
            .Select(s => new SessionSummary(s.Id, s.DeviceId, s.DeviceName, s.LastSeenAtUtc, s.AbsoluteExpiresAtUtc))
            .ToListAsync(cancellationToken);
    }
}
