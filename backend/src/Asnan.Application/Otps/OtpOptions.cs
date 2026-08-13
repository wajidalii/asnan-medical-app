namespace Asnan.Application.Otps;

public class OtpOptions
{
    public const string SectionName = "Otp";

    public int CodeLength { get; set; } = 6;

    public int ExpiryMinutes { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    public int ResendCooldownSeconds { get; set; } = 60;

    public int MaxRequestsPerHour { get; set; } = 5;

    /// <summary>
    /// Key for the keyed hash (HMAC-SHA256) codes are stored as. A 6-digit code
    /// has too little entropy for a bare unsalted hash to resist offline
    /// precomputation; keying it means an attacker needs this secret too, not
    /// just database read access.
    /// </summary>
    public string HashingKey { get; set; } = null!;
}
