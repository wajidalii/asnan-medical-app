using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// A scoped, short-lived credential proving OTP ownership of a destination
/// during signup — deliberately not a full session/JWT (see ARCHITECTURE.md
/// §4.1: "signup token", not a login). Single-use, hashed at rest like
/// <see cref="RefreshToken"/>.
/// </summary>
public class SignupToken : BaseEntity
{
    public string Destination { get; set; } = null!;

    public OtpChannel Channel { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}
