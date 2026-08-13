namespace Asnan.Application.Otps;

public enum OtpRequestStatus
{
    Sent,
    CooldownActive,
    RateLimited,
}

public record OtpRequestResult(OtpRequestStatus Status, TimeSpan? RetryAfter = null);

/// <summary>
/// <see cref="InvalidOrExpired"/> is deliberately the outcome for "wrong code",
/// "expired code", "no code was ever requested", and "too many attempts" alike —
/// the controller must not let a client distinguish between these (see
/// ARCHITECTURE.md §5: generic error messages, no enumeration signal). The
/// distinct statuses still exist here so tests can assert the right internal
/// rule fired.
/// </summary>
public enum OtpVerifyStatus
{
    Verified,
    InvalidOrExpired,
    TooManyAttempts,
}

public record OtpVerifyResult(OtpVerifyStatus Status);
