using Asnan.Application.Auth;
using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// Real-database concurrency proof for the refresh-token rotation race — see
/// RefreshTokenServiceTests' comment for why this can't be reproduced
/// faithfully against EF Core's InMemory provider.
/// </summary>
[Collection("Database")]
public class DbConcurrencyTests
{
    private static readonly JwtOptions JwtOptions = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "test-only-signing-key-at-least-32-characters-long",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenSlidingExpiryDays = 30,
        RefreshTokenAbsoluteExpiryDays = 90,
    };

    private readonly DatabaseFixture _fixture;

    public DbConcurrencyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private AsnanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(_fixture.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new AsnanDbContext(options);
    }

    private RefreshTokenService CreateService(AsnanDbContext db) =>
        new(db, new JwtTokenService(Options.Create(JwtOptions)), Options.Create(JwtOptions));

    [Fact]
    public async Task RefreshAsync_ConcurrentRotationOfSameToken_ExactlyOneWins()
    {
        var now = DateTime.UtcNow;
        string rawToken;

        await using (var seedDb = CreateDb())
        {
            var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            seedDb.Users.Add(user);
            seedDb.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });

            var session = new UserSession
            {
                UserId = user.Id,
                DeviceId = "concurrency-test-device",
                LastSeenAtUtc = now,
                AbsoluteExpiresAtUtc = now.AddDays(90),
            };
            seedDb.UserSessions.Add(session);

            rawToken = OpaqueTokenGenerator.Generate();
            seedDb.RefreshTokens.Add(new RefreshToken
            {
                UserSessionId = session.Id,
                TokenHash = OpaqueTokenGenerator.Hash(rawToken),
                ExpiresAtUtc = now.AddDays(30),
            });

            await seedDb.SaveChangesAsync();
        }

        await using var dbA = CreateDb();
        await using var dbB = CreateDb();
        var serviceA = CreateService(dbA);
        var serviceB = CreateService(dbB);

        var resultA = serviceA.RefreshAsync(rawToken);
        var resultB = serviceB.RefreshAsync(rawToken);
        await Task.WhenAll(resultA, resultB);

        var statuses = new[] { resultA.Result.Status, resultB.Result.Status };
        Assert.Contains(RefreshStatus.Success, statuses);
        Assert.Contains(RefreshStatus.ReuseDetected, statuses);
    }
}
