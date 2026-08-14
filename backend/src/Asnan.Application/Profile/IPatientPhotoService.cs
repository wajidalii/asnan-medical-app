namespace Asnan.Application.Profile;

public enum PhotoProcessingStatus
{
    Success,

    /// <summary>Not a real, decodable image regardless of declared content-type/extension — the actual magic-bytes check (ARCHITECTURE.md §13).</summary>
    InvalidImage,

    TooLarge,
}

public record PhotoProcessingResult(PhotoProcessingStatus Status);

/// <summary>
/// Photo validation/storage — issue #33. Swappable the same way
/// IPaymentProvider/INotificationSender are; only a local-disk
/// implementation exists today (LocalPatientPhotoService).
/// </summary>
public interface IPatientPhotoService
{
    /// <summary>
    /// Validates by actually decoding the image (rejects anything that
    /// isn't a real image regardless of what it claims to be — an
    /// executable renamed to .jpg fails here), then re-encodes to JPEG
    /// (strips EXIF/any embedded payload) and saves under a fixed
    /// per-user path — never a user-supplied filename.
    /// </summary>
    Task<PhotoProcessingResult> ProcessAndSaveAsync(Guid userId, Stream fileStream, long declaredLength, CancellationToken cancellationToken = default);

    /// <summary>Null if the user has no stored photo.</summary>
    Task<Stream?> OpenReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
