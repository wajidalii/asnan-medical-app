using Asnan.Domain.Entities;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Asnan.Api.Tests;

/// <summary>
/// Verifies the constraints ARCHITECTURE.md §12 calls out as the actual
/// source of truth for data integrity — not just application-level checks.
/// Runs against a real MySQL/MariaDB instance (never in-memory), since the
/// whole point is proving the database itself rejects the bad state.
///
/// Connection string comes from ConnectionStrings__Default (same convention
/// the API uses); CI provides a MySQL service container, see backend-ci.yml.
/// </summary>
public class DbConstraintTests : IAsyncLifetime
{
    private AsnanDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Port=3307;Database=asnan_dev;User=asnan;Password=asnan_dev_only_password;";

        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        _db = new AsnanDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task DuplicateRefreshTokenHash_IsRejectedByUniqueConstraint()
    {
        var user = new User { Email = $"{Guid.NewGuid()}@test.local" };
        var session = new UserSession
        {
            UserId = user.Id,
            User = user,
            DeviceId = "device-1",
            AbsoluteExpiresAtUtc = DateTime.UtcNow.AddDays(90),
        };
        _db.Users.Add(user);
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();

        var sharedHash = $"hash-{Guid.NewGuid()}";

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserSessionId = session.Id,
            TokenHash = sharedHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserSessionId = session.Id,
            TokenHash = sharedHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateUserEmail_IsRejectedByUniqueConstraint()
    {
        var sharedEmail = $"{Guid.NewGuid()}@test.local";

        _db.Users.Add(new User { Email = sharedEmail });
        await _db.SaveChangesAsync();

        _db.Users.Add(new User { Email = sharedEmail });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task MultipleUsersWithNullEmail_AreAllowed()
    {
        // MySQL/MariaDB unique indexes treat each NULL as distinct — this is
        // what makes "unique when present" work without a filtered index.
        _db.Users.Add(new User { Mobile = $"+1{Guid.NewGuid():N}"[..12] });
        _db.Users.Add(new User { Mobile = $"+1{Guid.NewGuid():N}"[..12] });

        await _db.SaveChangesAsync();
    }
}
