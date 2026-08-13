namespace Asnan.Domain.Entities;

/// <summary>
/// Fixed, small reference set (Patient, Doctor, Admin) — seeded, not
/// user-creatable, so it doesn't need the audit/soft-delete fields of
/// <see cref="Common.BaseEntity"/>.
/// </summary>
public class Role
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public static class RoleIds
{
    public const int Patient = 1;
    public const int Doctor = 2;
    public const int Admin = 3;
}
