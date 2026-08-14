using Asnan.Application.Profile;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Asnan.Infrastructure.Profile;

/// <summary>
/// Local-disk photo storage — issue #33. Only a local implementation
/// exists today; a real deployment target (S3-compatible blob storage) can
/// register behind the same <see cref="IPatientPhotoService"/> later, same
/// pattern as the other provider seams in this codebase.
///
/// The actual security guarantee (ARCHITECTURE.md §13) lives here: a
/// decode failure — not a Content-Type header, not a file extension — is
/// what "not a real image" means, so an executable renamed to .jpg is
/// rejected by <see cref="SKBitmap.Decode(Stream)"/> returning null.
/// Re-encoding to JPEG afterward strips any embedded metadata/payload the
/// original file carried; the saved file is never a byte-for-byte copy of
/// what was uploaded. Storage path is always <c>{userId:N}.jpg</c> — never
/// a user-supplied filename, so there is no path-traversal surface.
/// </summary>
public class LocalPatientPhotoService : IPatientPhotoService
{
    private const int MaxDimensionPixels = 1024;
    private const int JpegQuality = 85;

    private readonly string _rootPath;
    private readonly int _maxSizeBytes;

    public LocalPatientPhotoService(IOptions<PhotoStorageOptions> options, IHostEnvironment environment)
    {
        var configuredRoot = options.Value.RootPath;
        _rootPath = Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(environment.ContentRootPath, configuredRoot);
        _maxSizeBytes = options.Value.MaxSizeBytes;

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<PhotoProcessingResult> ProcessAndSaveAsync(Guid userId, Stream fileStream, long declaredLength, CancellationToken cancellationToken = default)
    {
        if (declaredLength > _maxSizeBytes)
        {
            return new PhotoProcessingResult(PhotoProcessingStatus.TooLarge);
        }

        using var buffered = new MemoryStream();
        if (!await TryBufferWithinLimitAsync(fileStream, buffered, cancellationToken))
        {
            return new PhotoProcessingResult(PhotoProcessingStatus.TooLarge);
        }

        buffered.Position = 0;
        using var bitmap = SKBitmap.Decode(buffered);
        if (bitmap is null)
        {
            return new PhotoProcessingResult(PhotoProcessingStatus.InvalidImage);
        }

        using var resized = Resize(bitmap);
        using var image = SKImage.FromBitmap(resized);
        using var jpegData = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        await using var fileOutput = File.Create(PathFor(userId));
        jpegData.SaveTo(fileOutput);

        return new PhotoProcessingResult(PhotoProcessingStatus.Success);
    }

    public Task<Stream?> OpenReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(userId);
        return Task.FromResult(File.Exists(path) ? File.OpenRead(path) as Stream : null);
    }

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(userId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Caps actual bytes copied regardless of the caller's declared length — a lied-about Content-Length must not let an oversized payload through or buffer unbounded memory.</summary>
    private async Task<bool> TryBufferWithinLimitAsync(Stream source, MemoryStream destination, CancellationToken cancellationToken)
    {
        var copyBuffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(copyBuffer, cancellationToken)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > _maxSizeBytes)
            {
                return false;
            }

            await destination.WriteAsync(copyBuffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return true;
    }

    private string PathFor(Guid userId) => Path.Combine(_rootPath, $"{userId:N}.jpg");

    private static SKBitmap Resize(SKBitmap original)
    {
        if (original.Width <= MaxDimensionPixels && original.Height <= MaxDimensionPixels)
        {
            return original.Copy();
        }

        var scale = (double)MaxDimensionPixels / Math.Max(original.Width, original.Height);
        var newWidth = Math.Max(1, (int)(original.Width * scale));
        var newHeight = Math.Max(1, (int)(original.Height * scale));

        return original.Resize(new SKSizeI(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
    }
}
