namespace Asnan.Application.Auth;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(Guid userId, IEnumerable<string> roles);
}
