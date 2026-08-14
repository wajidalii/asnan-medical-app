using Asnan.Domain.Common;
using Asnan.Domain.Enums;

namespace Asnan.Domain.Entities;

/// <summary>
/// Patient-specific profile data for a <see cref="User"/> — issue #33.
/// Deliberately minimal per prompt.md's "User Profile" section and
/// ARCHITECTURE.md's data-minimization stance: no medical fields (no
/// diagnosis/visit-reason/history anywhere in this system).
///
/// Created lazily on first write (PatientProfileService.UpdateProfileAsync
/// upserts), not at signup — a user who never visits the profile screen
/// never gets a row, same "don't create until needed" precedent as
/// NotificationPreference.
///
/// Identity fields (email/mobile, the verified login identifiers) live on
/// <see cref="User"/>, not duplicated here — <see cref="Phone"/> below is a
/// separate, unverified contact number a patient may optionally add.
/// </summary>
public class PatientProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public Gender? Gender { get; set; }

    /// <summary>Optional contact number — distinct from the verified login <see cref="User.Mobile"/>.</summary>
    public string? Phone { get; set; }

    public string? AddressLine { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    /// <summary>
    /// Null if no photo has ever been uploaded. Doubles as a cache-busting
    /// value for the photo URL client-side. The photo itself is stored
    /// outside this row (see IPatientPhotoService) — always re-encoded to
    /// JPEG and always saved under a fixed per-user path, never a
    /// user-supplied filename (ARCHITECTURE.md §13).
    /// </summary>
    public DateTime? PhotoUpdatedAtUtc { get; set; }
}
