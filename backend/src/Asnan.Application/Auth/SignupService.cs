using Asnan.Application.Auditing;
using Asnan.Application.Common;
using Asnan.Application.Otps;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Auth;

public class SignupService : ISignupService
{
    private static readonly TimeSpan SignupTokenLifetime = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogger _auditLogger;

    public SignupService(IApplicationDbContext db, IOtpService otpService, IPasswordHasher passwordHasher, IAuditLogger auditLogger)
    {
        _db = db;
        _otpService = otpService;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
    }

    public Task<OtpRequestResult> RequestOtpAsync(string destination, OtpChannel channel, CancellationToken cancellationToken = default) =>
        _otpService.RequestAsync(destination, channel, OtpPurpose.SignupVerification, cancellationToken);

    public async Task<SignupVerifyOtpResult> VerifyOtpAsync(
        string destination, string code, OtpChannel channel, CancellationToken cancellationToken = default)
    {
        var verify = await _otpService.VerifyAsync(destination, code, OtpPurpose.SignupVerification, cancellationToken);
        if (verify.Status != OtpVerifyStatus.Verified)
        {
            return new SignupVerifyOtpResult(false, null);
        }

        var rawToken = OpaqueTokenGenerator.Generate();
        _db.SignupTokens.Add(new SignupToken
        {
            Destination = destination,
            Channel = channel,
            TokenHash = OpaqueTokenGenerator.Hash(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.Add(SignupTokenLifetime),
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new SignupVerifyOtpResult(true, rawToken);
    }

    public async Task<SetPasswordResult> SetPasswordAsync(
        string signupToken, string password, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokenHash = OpaqueTokenGenerator.Hash(signupToken);

        var token = await _db.SignupTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now,
            cancellationToken);

        if (token is null)
        {
            return new SetPasswordResult(SetPasswordStatus.InvalidOrExpiredToken);
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(
            u => (token.Channel == OtpChannel.Email && u.Email == token.Destination)
                || (token.Channel == OtpChannel.Sms && u.Mobile == token.Destination),
            cancellationToken);

        if (existingUser is not null)
        {
            // The caller already proved destination ownership via OTP to get this far,
            // so revealing the account exists here isn't an enumeration risk the way it
            // would be at the request-OTP step. Still consume the token either way.
            token.ConsumedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new SetPasswordResult(SetPasswordStatus.AccountAlreadyExists);
        }

        var user = new User
        {
            Email = token.Channel == OtpChannel.Email ? token.Destination : null,
            Mobile = token.Channel == OtpChannel.Sms ? token.Destination : null,
            PasswordHash = _passwordHasher.Hash(password),
            EmailVerifiedAtUtc = token.Channel == OtpChannel.Email ? now : null,
            MobileVerifiedAtUtc = token.Channel == OtpChannel.Sms ? now : null,
        };
        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });
        token.ConsumedAtUtc = now;

        _auditLogger.Record(user.Id, "PasswordSet", "Initial password set during signup.");

        await _db.SaveChangesAsync(cancellationToken);

        return new SetPasswordResult(SetPasswordStatus.Success, user.Id);
    }
}
