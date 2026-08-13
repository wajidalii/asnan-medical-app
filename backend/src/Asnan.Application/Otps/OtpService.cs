using System.Security.Cryptography;
using System.Text;
using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Application.Otps;

public class OtpService : IOtpService
{
    private readonly IApplicationDbContext _db;
    private readonly IOtpSender _sender;
    private readonly OtpOptions _options;

    public OtpService(IApplicationDbContext db, IOtpSender sender, IOptions<OtpOptions> options)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
    }

    public async Task<OtpRequestResult> RequestAsync(
        string destination,
        OtpChannel channel,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddHours(-1);

        var recentCount = await _db.Otps.CountAsync(
            o => o.Destination == destination && o.Purpose == purpose && o.CreatedAtUtc >= windowStart,
            cancellationToken);

        if (recentCount >= _options.MaxRequestsPerHour)
        {
            return new OtpRequestResult(OtpRequestStatus.RateLimited);
        }

        var lastSent = await _db.Otps
            .Where(o => o.Destination == destination && o.Purpose == purpose)
            .OrderByDescending(o => o.LastSentAtUtc)
            .Select(o => (DateTime?)o.LastSentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSent is { } last)
        {
            var elapsed = now - last;
            var cooldown = TimeSpan.FromSeconds(_options.ResendCooldownSeconds);
            if (elapsed < cooldown)
            {
                return new OtpRequestResult(OtpRequestStatus.CooldownActive, cooldown - elapsed);
            }
        }

        var code = GenerateCode(_options.CodeLength);

        var otp = new Otp
        {
            Destination = destination,
            Purpose = purpose,
            CodeHash = Hash(code),
            ExpiresAtUtc = now.AddMinutes(_options.ExpiryMinutes),
            MaxAttempts = _options.MaxAttempts,
            LastSentAtUtc = now,
        };
        _db.Otps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        await _sender.SendAsync(destination, code, channel, cancellationToken);

        return new OtpRequestResult(OtpRequestStatus.Sent);
    }

    public async Task<OtpVerifyResult> VerifyAsync(
        string destination,
        string code,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var otp = await _db.Otps
            .Where(o => o.Destination == destination
                && o.Purpose == purpose
                && o.ConsumedAtUtc == null
                && o.ExpiresAtUtc > now)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return new OtpVerifyResult(OtpVerifyStatus.InvalidOrExpired);
        }

        if (otp.AttemptCount >= otp.MaxAttempts)
        {
            otp.ConsumedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerifyResult(OtpVerifyStatus.TooManyAttempts);
        }

        if (FixedTimeEquals(Hash(code), otp.CodeHash))
        {
            otp.ConsumedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerifyResult(OtpVerifyStatus.Verified);
        }

        otp.AttemptCount++;
        if (otp.AttemptCount >= otp.MaxAttempts)
        {
            otp.ConsumedAtUtc = now;
        }
        await _db.SaveChangesAsync(cancellationToken);

        return new OtpVerifyResult(OtpVerifyStatus.InvalidOrExpired);
    }

    private static string GenerateCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', length));
    }

    private string Hash(string code)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.HashingKey));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }
}
