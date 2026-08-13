namespace Asnan.Application.Auth;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
}

public record LoginResult(
    LoginStatus Status,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAtUtc = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAtUtc = null);
