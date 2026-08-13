using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// Short-TTL claim on a slot while a patient completes booking/payment —
/// ARCHITECTURE.md §6. The DB-level uniqueness constraint on (DoctorProfileId,
/// SlotStartUtc) among Active holds — not this class's logic — is the actual
/// source of truth that makes double-booking impossible under concurrency;
/// see AppointmentHoldConfiguration for how that's expressed in MySQL.
/// </summary>
public class AppointmentHold : BaseEntity
{
    public Guid DoctorProfileId { get; set; }

    public DoctorProfile DoctorProfile { get; set; } = null!;

    public Guid PatientUserId { get; set; }

    public User PatientUser { get; set; } = null!;

    public DateTime SlotStartUtc { get; set; }

    public DateTime SlotEndUtc { get; set; }

    public HoldStatus Status { get; set; } = HoldStatus.Active;

    public DateTime ExpiresAtUtc { get; set; }

    public string HoldTokenHash { get; set; } = null!;

    /// <summary>
    /// "{DoctorProfileId}|{SlotStartUtc:O}" when <see cref="Status"/> is
    /// Active, null otherwise — maintained by the application (see
    /// AppointmentHoldService), not a DB-computed column. A unique index on
    /// this column is what makes a concurrent second insert for the same
    /// doctor+slot fail at the DB level; MySQL/MariaDB have no native
    /// filtered/partial unique index, so this is the "NULL is distinct"
    /// trick already used for Users.Email/Mobile, done in the application
    /// layer instead of a generated column — MariaDB (used in local dev,
    /// unlike CI's real MySQL 8) rejects string functions like CONCAT in
    /// generated-column expressions, so a DB-computed version isn't portable.
    /// </summary>
    public string? ActiveSlotKey { get; set; }
}
