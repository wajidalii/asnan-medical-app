using Asnan.Application.Auditing;
using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Application.Auth;

public class LoginService : ILoginService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditLogger _auditLogger;
    private readonly JwtOptions _options;

    public LoginService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuditLogger auditLogger,
        IOptions<JwtOptions> options)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
        _options = options.Value;
    }

    public async Task<LoginResult> LoginAsync(
        string identifier,
        string password,
        string deviceId,
        string? deviceName,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == identifier || u.Mobile == identifier, cancellationToken);

        // Same generic failure for "no such user", "signup never completed
        // (no password set)", and "wrong password" — no signal to the caller
        // about which case it was (§4.2: no enumeration via login either).
        if (user is null || user.PasswordHash is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            // identifier (an email/mobile), not a denylisted field — this is
            // exactly what an audit trail needs to record (ARCHITECTURE.md §13).
            _auditLogger.Record(user?.Id, "LoginFailed", $"identifier={identifier}");
            await _db.SaveChangesAsync(cancellationToken);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var now = DateTime.UtcNow;

        var session = new UserSession
        {
            UserId = user.Id,
            DeviceId = deviceId,
            DeviceName = deviceName,
            LastSeenAtUtc = now,
            AbsoluteExpiresAtUtc = now.AddDays(_options.RefreshTokenAbsoluteExpiryDays),
        };
        _db.UserSessions.Add(session);

        var rawRefreshToken = OpaqueTokenGenerator.Generate();
        var refreshTokenExpiresAtUtc = now.AddDays(_options.RefreshTokenSlidingExpiryDays);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserSession = session,
            TokenHash = OpaqueTokenGenerator.Hash(rawRefreshToken),
            ExpiresAtUtc = refreshTokenExpiresAtUtc,
        });

        var roles = user.UserRoles.Select(ur => ur.Role.Name);
        var (accessToken, accessTokenExpiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, roles);

        _auditLogger.Record(user.Id, "LoginSucceeded", $"deviceId={deviceId}");

        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            LoginStatus.Success,
            accessToken,
            accessTokenExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAtUtc);
    }
}
