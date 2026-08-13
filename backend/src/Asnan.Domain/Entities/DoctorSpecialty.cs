namespace Asnan.Domain.Entities;

/// <summary>Join entity, composite key — same shape as <see cref="UserRole"/>.</summary>
public class DoctorSpecialty
{
    public Guid DoctorProfileId { get; set; }

    public DoctorProfile DoctorProfile { get; set; } = null!;

    public Guid SpecialtyId { get; set; }

    public Specialty Specialty { get; set; } = null!;
}
