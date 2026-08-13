using Asnan.Application.Auth;
using Asnan.Application.Otps;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// Business-rule tests for <see cref="SignupService"/>, EF Core InMemory —
/// see OtpServiceTests' remarks for why (constraint-level behavior is
/// covered separately against a real database).
/// </summary>
public class SignupServiceTests
{
    private sealed class FakeOtpSender : IOtpSender
    {
        public readonly List<(string Destination, string Code)> Sent = new();

        public Task SendAsync(string destination, string code, OtpChannel channel, CancellationToken cancellationToken = default)
        {
            Sent.Add((destination, code));
            return Task.CompletedTask;
        }
    }

    private static (AsnanDbContext Db, FakeOtpSender Sender, SignupService Signup) CreateService()
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AsnanDbContext(options);

        var otpOptions = Options.Create(new OtpOptions
        {
            CodeLength = 6,
            ExpiryMinutes = 5,
            MaxAttempts = 5,
            ResendCooldownSeconds = 0,
            MaxRequestsPerHour = 100,
            HashingKey = "test-only-hashing-key",
        });
        var sender = new FakeOtpSender();
        var otpService = new OtpService(db, sender, otpOptions);
        var signupService = new SignupService(db, otpService, new Pbkdf2PasswordHasher());

        return (db, sender, signupService);
    }

    [Fact]
    public async Task FullFlow_RequestVerifySetPassword_CreatesPatientUser()
    {
        var (db, sender, signup) = CreateService();
        const string destination = "new-patient@test.local";

        await signup.RequestOtpAsync(destination, OtpChannel.Email);
        var code = sender.Sent[0].Code;

        var verify = await signup.VerifyOtpAsync(destination, code, OtpChannel.Email);
        Assert.True(verify.Verified);
        Assert.NotNull(verify.SignupToken);

        var result = await signup.SetPasswordAsync(verify.SignupToken!, "correct horse battery staple");

        Assert.Equal(SetPasswordStatus.Success, result.Status);
        Assert.NotNull(result.UserId);

        var user = await db.Users.SingleAsync(u => u.Id == result.UserId);
        Assert.Equal(destination, user.Email);
        Assert.NotNull(user.EmailVerifiedAtUtc);
        Assert.NotNull(user.PasswordHash);

        var role = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
        Assert.Equal(RoleIds.Patient, role.RoleId);
    }

    [Fact]
    public async Task SetPasswordAsync_SignupTokenIsSingleUse()
    {
        var (_, sender, signup) = CreateService();
        const string destination = "single-use@test.local";
        await signup.RequestOtpAsync(destination, OtpChannel.Email);
        var verify = await signup.VerifyOtpAsync(destination, sender.Sent[0].Code, OtpChannel.Email);

        var first = await signup.SetPasswordAsync(verify.SignupToken!, "correct horse battery staple");
        var second = await signup.SetPasswordAsync(verify.SignupToken!, "another-fine-password-99");

        Assert.Equal(SetPasswordStatus.Success, first.Status);
        Assert.Equal(SetPasswordStatus.InvalidOrExpiredToken, second.Status);
    }

    [Fact]
    public async Task SetPasswordAsync_UnknownToken_ReturnsInvalidOrExpired()
    {
        var (_, _, signup) = CreateService();

        var result = await signup.SetPasswordAsync("not-a-real-token", "correct horse battery staple");

        Assert.Equal(SetPasswordStatus.InvalidOrExpiredToken, result.Status);
    }

    [Fact]
    public async Task SetPasswordAsync_DestinationAlreadyRegistered_ReturnsConflict_AndStillConsumesToken()
    {
        var (db, sender, signup) = CreateService();
        const string destination = "existing@test.local";
        db.Users.Add(new User { Email = destination, PasswordHash = "irrelevant" });
        await db.SaveChangesAsync();

        await signup.RequestOtpAsync(destination, OtpChannel.Email);
        var verify = await signup.VerifyOtpAsync(destination, sender.Sent[0].Code, OtpChannel.Email);

        var result = await signup.SetPasswordAsync(verify.SignupToken!, "correct horse battery staple");
        var replay = await signup.SetPasswordAsync(verify.SignupToken!, "correct horse battery staple");

        Assert.Equal(SetPasswordStatus.AccountAlreadyExists, result.Status);
        Assert.Equal(SetPasswordStatus.InvalidOrExpiredToken, replay.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_WrongCode_DoesNotIssueSignupToken()
    {
        var (_, sender, signup) = CreateService();
        const string destination = "wrong-code@test.local";
        await signup.RequestOtpAsync(destination, OtpChannel.Email);
        var wrongCode = sender.Sent[0].Code == "000000" ? "111111" : "000000";

        var result = await signup.VerifyOtpAsync(destination, wrongCode, OtpChannel.Email);

        Assert.False(result.Verified);
        Assert.Null(result.SignupToken);
    }
}
