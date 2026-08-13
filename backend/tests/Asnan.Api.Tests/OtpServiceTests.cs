using Asnan.Application.Otps;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// Business-rule tests for <see cref="OtpService"/>. Uses EF Core's InMemory
/// provider — fast and isolated — because these tests exercise
/// application-layer rules (expiry/attempts/cooldown/rate-limit logic), not
/// the database's own constraints; those are covered separately by
/// <see cref="DbConstraintTests"/> against a real database.
/// </summary>
public class OtpServiceTests
{
    private sealed class FakeOtpSender : IOtpSender
    {
        public readonly List<(string Destination, string Code, OtpChannel Channel)> Sent = new();

        public Task SendAsync(string destination, string code, OtpChannel channel, CancellationToken cancellationToken = default)
        {
            Sent.Add((destination, code, channel));
            return Task.CompletedTask;
        }
    }

    private static (AsnanDbContext Db, FakeOtpSender Sender, OtpService Service) CreateService(
        Action<OtpOptions>? configure = null)
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AsnanDbContext(options);

        var otpOptions = new OtpOptions
        {
            CodeLength = 6,
            ExpiryMinutes = 5,
            MaxAttempts = 5,
            ResendCooldownSeconds = 60,
            MaxRequestsPerHour = 5,
            HashingKey = "test-only-hashing-key",
        };
        configure?.Invoke(otpOptions);

        var sender = new FakeOtpSender();
        var service = new OtpService(db, sender, Options.Create(otpOptions));

        return (db, sender, service);
    }

    [Fact]
    public async Task RequestAsync_SendsCode_AndPersistsOnlyItsHash()
    {
        var (db, sender, service) = CreateService();

        var result = await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);

        Assert.Equal(OtpRequestStatus.Sent, result.Status);
        Assert.Single(sender.Sent);

        var sentCode = sender.Sent[0].Code;
        var stored = await db.Otps.SingleAsync();
        Assert.NotEqual(sentCode, stored.CodeHash);
    }

    [Fact]
    public async Task RequestAsync_WithinCooldown_ReturnsCooldownActive()
    {
        var (_, _, service) = CreateService(o => o.ResendCooldownSeconds = 60);

        var first = await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);
        var second = await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);

        Assert.Equal(OtpRequestStatus.Sent, first.Status);
        Assert.Equal(OtpRequestStatus.CooldownActive, second.Status);
        Assert.NotNull(second.RetryAfter);
    }

    [Fact]
    public async Task RequestAsync_ExceedingHourlyLimit_ReturnsRateLimited()
    {
        var (_, _, service) = CreateService(o =>
        {
            o.MaxRequestsPerHour = 1;
            o.ResendCooldownSeconds = 0;
        });

        var first = await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);
        var second = await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);

        Assert.Equal(OtpRequestStatus.Sent, first.Status);
        Assert.Equal(OtpRequestStatus.RateLimited, second.Status);
    }

    [Fact]
    public async Task VerifyAsync_CorrectCode_SucceedsOnce_ThenRejectsReplay()
    {
        var (_, sender, service) = CreateService();
        await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);
        var code = sender.Sent[0].Code;

        var firstVerify = await service.VerifyAsync("user@test.local", code, OtpPurpose.SignupVerification);
        var replay = await service.VerifyAsync("user@test.local", code, OtpPurpose.SignupVerification);

        Assert.Equal(OtpVerifyStatus.Verified, firstVerify.Status);
        Assert.Equal(OtpVerifyStatus.InvalidOrExpired, replay.Status);
    }

    [Fact]
    public async Task VerifyAsync_WrongCode_RepeatedlyLocksOutAfterMaxAttempts()
    {
        var (_, sender, service) = CreateService(o => o.MaxAttempts = 2);
        await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);
        var correctCode = sender.Sent[0].Code;
        var wrongCode = correctCode == "000000" ? "111111" : "000000";

        var attempt1 = await service.VerifyAsync("user@test.local", wrongCode, OtpPurpose.SignupVerification);
        var attempt2 = await service.VerifyAsync("user@test.local", wrongCode, OtpPurpose.SignupVerification);
        // Code is now invalidated even though this attempt uses the correct code.
        var attempt3 = await service.VerifyAsync("user@test.local", correctCode, OtpPurpose.SignupVerification);

        Assert.Equal(OtpVerifyStatus.InvalidOrExpired, attempt1.Status);
        Assert.Equal(OtpVerifyStatus.InvalidOrExpired, attempt2.Status);
        Assert.Equal(OtpVerifyStatus.InvalidOrExpired, attempt3.Status);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredCode_ReturnsInvalidOrExpired()
    {
        var (db, sender, service) = CreateService();
        await service.RequestAsync("user@test.local", OtpChannel.Email, OtpPurpose.SignupVerification);
        var code = sender.Sent[0].Code;

        var stored = await db.Otps.SingleAsync();
        stored.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await service.VerifyAsync("user@test.local", code, OtpPurpose.SignupVerification);

        Assert.Equal(OtpVerifyStatus.InvalidOrExpired, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_NoCodeEverRequested_ReturnsInvalidOrExpired()
    {
        var (_, _, service) = CreateService();

        var result = await service.VerifyAsync("nobody@test.local", "123456", OtpPurpose.SignupVerification);

        Assert.Equal(OtpVerifyStatus.InvalidOrExpired, result.Status);
    }
}
