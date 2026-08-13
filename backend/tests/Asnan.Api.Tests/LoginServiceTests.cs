using Asnan.Application.Auth;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

public class LoginServiceTests
{
    private static (AsnanDbContext Db, LoginService Service, JwtOptions Options) CreateService()
    {
        var dbOptions = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AsnanDbContext(dbOptions);

        var jwtOptions = new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-only-signing-key-at-least-32-characters-long",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenSlidingExpiryDays = 30,
            RefreshTokenAbsoluteExpiryDays = 90,
        };
        var optionsWrapper = Options.Create(jwtOptions);

        var hasher = new Pbkdf2PasswordHasher();
        var jwtService = new JwtTokenService(optionsWrapper);
        var service = new LoginService(db, hasher, jwtService, optionsWrapper);

        return (db, service, jwtOptions);
    }

    private static async Task<User> SeedUserAsync(AsnanDbContext db, string email, string password)
    {
        var hasher = new Pbkdf2PasswordHasher();
        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            EmailVerifiedAtUtc = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });
        db.Roles.Add(new Role { Id = RoleIds.Patient, Name = "Patient" });
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokensAndCreatesSession()
    {
        var (db, service, _) = CreateService();
        await SeedUserAsync(db, "patient@test.local", "correct horse battery staple");

        var result = await service.LoginAsync("patient@test.local", "correct horse battery staple", "device-1", "Test Phone");

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));

        var session = await db.UserSessions.SingleAsync();
        Assert.Equal("device-1", session.DeviceId);
        Assert.Equal("Test Phone", session.DeviceName);

        var refreshToken = await db.RefreshTokens.SingleAsync();
        Assert.Equal(session.Id, refreshToken.UserSessionId);
        Assert.NotEqual(result.RefreshToken, refreshToken.TokenHash);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var (db, service, _) = CreateService();
        await SeedUserAsync(db, "patient@test.local", "correct horse battery staple");

        var result = await service.LoginAsync("patient@test.local", "wrong-password", "device-1", null);

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_UnknownIdentifier_ReturnsInvalidCredentials()
    {
        var (_, service, _) = CreateService();

        var result = await service.LoginAsync("nobody@test.local", "whatever-password", "device-1", null);

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public async Task LoginAsync_UserWithoutPasswordSet_ReturnsInvalidCredentials()
    {
        var (db, service, _) = CreateService();
        // Simulates a user mid-signup: OTP verified but set-password never completed.
        db.Users.Add(new User { Email = "incomplete@test.local", PasswordHash = null });
        await db.SaveChangesAsync();

        var result = await service.LoginAsync("incomplete@test.local", "any-password-at-all", "device-1", null);

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }
}
