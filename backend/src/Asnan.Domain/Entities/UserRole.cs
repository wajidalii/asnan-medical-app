namespace Asnan.Domain.Entities;

/// <summary>Join entity; composite key (UserId, RoleId), no surrogate Id needed.</summary>
public class UserRole
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public int RoleId { get; set; }

    public Role Role { get; set; } = null!;
}
