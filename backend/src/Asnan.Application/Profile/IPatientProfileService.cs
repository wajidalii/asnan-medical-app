namespace Asnan.Application.Profile;

/// <summary>Object-level authorized to the caller's own profile only (no admin override) — issue #33.</summary>
public interface IPatientProfileService
{
    /// <summary>Synthesizes a default (empty) profile if the user has never saved one — see PatientProfile's doc comment. Never returns null.</summary>
    Task<PatientProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Upserts — creates the row on first save.</summary>
    Task<PatientProfileDto> UpdateProfileAsync(Guid userId, UpdatePatientProfileDto dto, CancellationToken cancellationToken = default);

    Task<PhotoProcessingResult> UploadPhotoAsync(Guid userId, Stream fileStream, long declaredLength, CancellationToken cancellationToken = default);

    Task<Stream?> GetPhotoAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the account (Users.DeletedAtUtc) and revokes every
    /// session — ARCHITECTURE.md §13's retention stance. Hard deletion is a
    /// separate, later data-retention job operating on already-soft-deleted
    /// rows, not implemented here (flagged, not faked, same as other
    /// deliberately-deferred pieces in this codebase).
    /// </summary>
    Task RequestAccountDeletionAsync(Guid userId, CancellationToken cancellationToken = default);
}
