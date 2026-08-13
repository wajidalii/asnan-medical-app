using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// Per ARCHITECTURE.md §5. <see cref="CodeHash"/> — never the raw code — is
/// stored; <see cref="AttemptCount"/>/<see cref="MaxAttempts"/> enforce
/// brute-force protection; <see cref="ConsumedAtUtc"/> enforces one-time use.
/// </summary>
public class Otp : BaseEntity
{
    /// <summary>Email address or mobile number the code was sent to.</summary>
    public string Destination { get; set; } = null!;

    public OtpPurpose Purpose { get; set; }

    public string CodeHash { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>Drives the resend cooldown.</summary>
    public DateTime LastSentAtUtc { get; set; } = DateTime.UtcNow;
}
