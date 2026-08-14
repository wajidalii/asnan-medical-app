using Asnan.Application.Auth;
using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Profile;

public class PatientProfileService : IPatientProfileService
{
    private readonly IApplicationDbContext _db;
    private readonly IPatientPhotoService _photoService;
    private readonly IRefreshTokenService _refreshTokenService;

    public PatientProfileService(IApplicationDbContext db, IPatientPhotoService photoService, IRefreshTokenService refreshTokenService)
    {
        _db = db;
        _photoService = photoService;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<PatientProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return ToDto(user, profile);
    }

    public async Task<PatientProfileDto> UpdateProfileAsync(Guid userId, UpdatePatientProfileDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = new PatientProfile { UserId = userId };
            _db.PatientProfiles.Add(profile);
        }

        profile.FullName = dto.FullName;
        profile.DateOfBirth = dto.DateOfBirth;
        profile.Gender = dto.Gender;
        profile.Phone = dto.Phone;
        profile.AddressLine = dto.AddressLine;
        profile.EmergencyContactName = dto.EmergencyContactName;
        profile.EmergencyContactPhone = dto.EmergencyContactPhone;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(user, profile);
    }

    public async Task<PhotoProcessingResult> UploadPhotoAsync(Guid userId, Stream fileStream, long declaredLength, CancellationToken cancellationToken = default)
    {
        var result = await _photoService.ProcessAndSaveAsync(userId, fileStream, declaredLength, cancellationToken);
        if (result.Status != PhotoProcessingStatus.Success)
        {
            return result;
        }

        var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new PatientProfile { UserId = userId };
            _db.PatientProfiles.Add(profile);
        }

        profile.PhotoUpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return result;
    }

    public Task<Stream?> GetPhotoAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _photoService.OpenReadAsync(userId, cancellationToken);

    public async Task RequestAccountDeletionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        user.DeletedAtUtc = DateTime.UtcNow;
        user.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        // Ends every existing session so a token minted before deletion can't keep working — see LoginService's matching gate on new logins.
        await _refreshTokenService.LogoutAllAsync(userId, cancellationToken);

        await _photoService.DeleteAsync(userId, cancellationToken);
    }

    private static PatientProfileDto ToDto(User user, PatientProfile? profile) => new(
        user.Id,
        user.Email,
        user.Mobile,
        profile?.FullName ?? string.Empty,
        profile?.DateOfBirth,
        profile?.Gender,
        profile?.Phone,
        profile?.AddressLine,
        profile?.EmergencyContactName,
        profile?.EmergencyContactPhone,
        profile?.PhotoUpdatedAtUtc is not null);
}
