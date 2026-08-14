using Asnan.Application.Auth;
using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

public class RefreshTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "test-only-signing-key-at-least-32-characters-long",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenSlidingExpiryDays = 30,
        RefreshTokenAbsoluteExpiryDays = 90,
    };

    private static AsnanDbContext CreateDb(string dbName) =>
        new(new DbContextOptionsBuilder<AsnanDbContext>().UseInMemoryDatabase(dbName).Options);

    private static RefreshTokenService CreateService(IApplicationDbContext db) =>
        new(db, new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Options)), Microsoft.Extensions.Options.Options.Create(Options));

    private static async Task<(User User, UserSession Session, string RawRefreshToken)> SeedLoggedInUserAsync(
        AsnanDbContext db, DateTime? absoluteExpiresAtUtc = null)
    {
        var now = DateTime.UtcNow;
        var user = new User { Email = "patient@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.Roles.Add(new Role { Id = RoleIds.Patient, Name = "Patient" });
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });

        var session = new UserSession
        {
            UserId = user.Id,
            DeviceId = "device-1",
            LastSeenAtUtc = now,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc ?? now.AddDays(90),
        };
        db.UserSessions.Add(session);

        var raw = OpaqueTokenGenerator.Generate();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserSessionId = session.Id,
            TokenHash = OpaqueTokenGenerator.Hash(raw),
            ExpiresAtUtc = now.AddDays(30),
        });

        await db.SaveChangesAsync();
        return (user, session, raw);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesAndReturnsNewPair()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (_, session, rawToken) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var result = await service.RefreshAsync(rawToken);

        Assert.Equal(RefreshStatus.Success, result.Status);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotEqual(rawToken, result.RefreshToken);

        var tokens = await db.RefreshTokens.Where(t => t.UserSessionId == session.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.UsedAtUtc != null);
        Assert.Single(tokens, t => t.UsedAtUtc == null);
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_ReturnsInvalidToken()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var service = CreateService(db);

        var result = await service.RefreshAsync("not-a-real-token");

        Assert.Equal(RefreshStatus.InvalidToken, result.Status);
    }

    [Fact]
    public async Task RefreshAsync_ReplayOfAlreadyRotatedToken_RevokesSession_AndFutureRefreshFails()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (_, session, rawToken) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var firstRefresh = await service.RefreshAsync(rawToken);
        Assert.Equal(RefreshStatus.Success, firstRefresh.Status);

        // Replaying the now-rotated original token — the theft signal.
        var replay = await service.RefreshAsync(rawToken);
        Assert.Equal(RefreshStatus.ReuseDetected, replay.Status);

        var reloadedSession = await db.UserSessions.SingleAsync(s => s.Id == session.Id);
        Assert.NotNull(reloadedSession.RevokedAtUtc);

        // Even the legitimately-rotated new token is now dead, because the whole
        // session was revoked, not just the replayed token.
        var afterRevocation = await service.RefreshAsync(firstRefresh.RefreshToken!);
        Assert.Equal(RefreshStatus.SessionRevoked, afterRevocation.Status);
    }

    [Fact]
    public async Task RefreshAsync_AbsoluteExpiryExceeded_ReturnsSessionRevoked_EvenWithAnUnusedToken()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (_, _, rawToken) = await SeedLoggedInUserAsync(db, absoluteExpiresAtUtc: DateTime.UtcNow.AddSeconds(-1));
        var service = CreateService(db);

        var result = await service.RefreshAsync(rawToken);

        Assert.Equal(RefreshStatus.SessionRevoked, result.Status);
    }

    [Fact]
    public async Task RefreshAsync_StaleOriginalValue_IsTreatedAsReuseNotSilentSuccess()
    {
        // Simulates what a genuine concurrent-rotation race produces: a context
        // that loaded the token while UsedAtUtc was still null, but where the
        // row has since actually changed underneath it. EF Core InMemory doesn't
        // reproduce true cross-context races the way a real database's
        // transaction isolation does (verified against real MySQL/MariaDB in
        // DbConcurrencyTests instead), so this exercises the same
        // DbUpdateConcurrencyException handling path directly via the change
        // tracker rather than via two racing DbContext instances.
        var db = CreateDb(Guid.NewGuid().ToString());
        var (_, _, rawToken) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var token = await db.RefreshTokens.SingleAsync();
        var entry = db.Entry(token);

        // First rotation succeeds and marks the token used.
        var firstResult = await service.RefreshAsync(rawToken);
        Assert.Equal(RefreshStatus.Success, firstResult.Status);

        // Force the tracked entry's original value back to "unused," as if this
        // context had loaded the row before the other request's commit —
        // without this, EF Core would see original==current and skip the token
        // update entirely on the next SaveChanges.
        entry.OriginalValues[nameof(RefreshToken.UsedAtUtc)] = null;
        entry.State = EntityState.Modified;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task LogoutAsync_RevokesOnlyTheAssociatedSession()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (user, session, rawToken) = await SeedLoggedInUserAsync(db);
        var otherSession = new UserSession
        {
            UserId = user.Id,
            DeviceId = "device-2",
            LastSeenAtUtc = DateTime.UtcNow,
            AbsoluteExpiresAtUtc = DateTime.UtcNow.AddDays(90),
        };
        db.UserSessions.Add(otherSession);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.LogoutAsync(rawToken);

        var reloadedSession = await db.UserSessions.SingleAsync(s => s.Id == session.Id);
        var reloadedOther = await db.UserSessions.SingleAsync(s => s.Id == otherSession.Id);
        Assert.NotNull(reloadedSession.RevokedAtUtc);
        Assert.Null(reloadedOther.RevokedAtUtc);
    }

    [Fact]
    public async Task LogoutAllAsync_RevokesEverySessionForTheUser()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (user, _, _) = await SeedLoggedInUserAsync(db);
        db.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            DeviceId = "device-2",
            LastSeenAtUtc = DateTime.UtcNow,
            AbsoluteExpiresAtUtc = DateTime.UtcNow.AddDays(90),
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.LogoutAllAsync(user.Id);

        var sessions = await db.UserSessions.Where(s => s.UserId == user.Id).ToListAsync();
        Assert.All(sessions, s => Assert.NotNull(s.RevokedAtUtc));
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ExcludesRevokedSessions()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (user, session, _) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var beforeLogout = await service.GetActiveSessionsAsync(user.Id);
        Assert.Single(beforeLogout);

        session.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var afterLogout = await service.GetActiveSessionsAsync(user.Id);
        Assert.Empty(afterLogout);
    }

    [Fact]
    public async Task RevokeSessionAsync_OwnSession_RevokesItAndReturnsTrue()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (user, session, _) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var revoked = await service.RevokeSessionAsync(user.Id, session.Id);

        Assert.True(revoked);
        await db.Entry(session).ReloadAsync();
        Assert.NotNull(session.RevokedAtUtc);
    }

    [Fact]
    public async Task RevokeSessionAsync_AnotherUsersSession_ReturnsFalseAndDoesNotRevoke()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (_, session, _) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var revoked = await service.RevokeSessionAsync(Guid.NewGuid(), session.Id);

        Assert.False(revoked);
        await db.Entry(session).ReloadAsync();
        Assert.Null(session.RevokedAtUtc);
    }

    [Fact]
    public async Task RevokeSessionAsync_AlreadyRevokedSession_ReturnsFalse()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (user, session, _) = await SeedLoggedInUserAsync(db);
        session.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var revoked = await service.RevokeSessionAsync(user.Id, session.Id);

        Assert.False(revoked);
    }

    [Fact]
    public async Task RevokeSessionAsync_UnknownSessionId_ReturnsFalse()
    {
        var db = CreateDb(Guid.NewGuid().ToString());
        var (user, _, _) = await SeedLoggedInUserAsync(db);
        var service = CreateService(db);

        var revoked = await service.RevokeSessionAsync(user.Id, Guid.NewGuid());

        Assert.False(revoked);
    }
}
