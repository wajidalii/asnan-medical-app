namespace Asnan.Application.Auth;

public record SignupVerifyOtpResult(bool Verified, string? SignupToken);

public enum SetPasswordStatus
{
    Success,
    InvalidOrExpiredToken,
    AccountAlreadyExists,
}

public record SetPasswordResult(SetPasswordStatus Status, Guid? UserId = null);
