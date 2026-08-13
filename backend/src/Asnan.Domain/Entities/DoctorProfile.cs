using Asnan.Domain.Common;

namespace Asnan.Domain.Entities;

/// <summary>
/// Doctor-specific data for a <see cref="User"/> holding the Doctor role —
/// deliberately not a parallel auth entity, see ARCHITECTURE.md §2.2.
/// </summary>
public class DoctorProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Bio { get; set; }

    public decimal ConsultationFee { get; set; }

    /// <summary>ISO 4217 currency code. See ARCHITECTURE.md §15 — market/currency is an open product decision; kept per-doctor and configurable rather than hardcoded.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>IANA time zone id (e.g. "Asia/Karachi") — see ARCHITECTURE.md §12.</summary>
    public string TimeZoneId { get; set; } = null!;

    public int? YearsOfExperience { get; set; }

    public string? ClinicAddress { get; set; }

    public bool IsAcceptingNewPatients { get; set; } = true;

    public ICollection<DoctorSpecialty> DoctorSpecialties { get; set; } = new List<DoctorSpecialty>();
}
