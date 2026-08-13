using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

public class User : BaseEntity
{
    /// <summary>Nullable: a user may sign up with only a mobile number, or only an email.</summary>
    public string? Email { get; set; }

    /// <summary>Nullable: see <see cref="Email"/>.</summary>
    public string? Mobile { get; set; }

    /// <summary>
    /// Null until the signup password-creation step completes — see
    /// ARCHITECTURE.md §4.1. Never populated with anything but a salted hash.
    /// </summary>
    public string? PasswordHash { get; set; }

    public DateTime? EmailVerifiedAtUtc { get; set; }

    public DateTime? MobileVerifiedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
