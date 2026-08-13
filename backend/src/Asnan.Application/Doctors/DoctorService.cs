using Asnan.Application.Common;
using Asnan.Application.Specialties;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Doctors;

public class DoctorService : IDoctorService
{
    private readonly IApplicationDbContext _db;

    public DoctorService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<DoctorProfileDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var doctors = await _db.DoctorProfiles
            .Include(d => d.DoctorSpecialties).ThenInclude(ds => ds.Specialty)
            .OrderBy(d => d.FullName)
            .ToListAsync(cancellationToken);

        return doctors.Select(ToDto).ToList();
    }

    public async Task<DoctorProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles
            .Include(d => d.DoctorSpecialties).ThenInclude(ds => ds.Specialty)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return doctor is null ? null : ToDto(doctor);
    }

    public async Task<DoctorMutationResult> CreateAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == dto.UserId, cancellationToken);
        if (user is null)
        {
            return new DoctorMutationResult(DoctorMutationStatus.UserNotFound);
        }

        if (await _db.DoctorProfiles.AnyAsync(d => d.UserId == dto.UserId, cancellationToken))
        {
            return new DoctorMutationResult(DoctorMutationStatus.ProfileAlreadyExists);
        }

        var specialties = await _db.Specialties.Where(s => dto.SpecialtyIds.Contains(s.Id)).ToListAsync(cancellationToken);
        if (specialties.Count != dto.SpecialtyIds.Distinct().Count())
        {
            return new DoctorMutationResult(DoctorMutationStatus.SpecialtyNotFound);
        }

        // Doctors are Users with the Doctor role plus a profile, not a parallel
        // auth entity (ARCHITECTURE.md §2.2) — attaching a profile is how an
        // admin promotes an existing signed-up user to also being a doctor.
        if (user.UserRoles.All(ur => ur.RoleId != RoleIds.Doctor))
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Doctor });
        }

        var doctor = new DoctorProfile
        {
            UserId = dto.UserId,
            FullName = dto.FullName,
            Bio = dto.Bio,
            ConsultationFee = dto.ConsultationFee,
            Currency = dto.Currency,
            TimeZoneId = dto.TimeZoneId,
            YearsOfExperience = dto.YearsOfExperience,
            ClinicAddress = dto.ClinicAddress,
            IsAcceptingNewPatients = dto.IsAcceptingNewPatients,
        };
        doctor.DoctorSpecialties = specialties.Select(s => new DoctorSpecialty { DoctorProfile = doctor, SpecialtyId = s.Id, Specialty = s }).ToList();

        _db.DoctorProfiles.Add(doctor);
        await _db.SaveChangesAsync(cancellationToken);

        return new DoctorMutationResult(DoctorMutationStatus.Success, ToDto(doctor));
    }

    public async Task<DoctorMutationResult> UpdateAsync(Guid id, UpdateDoctorDto dto, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles
            .Include(d => d.DoctorSpecialties).ThenInclude(ds => ds.Specialty)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doctor is null)
        {
            return new DoctorMutationResult(DoctorMutationStatus.NotFound);
        }

        var specialties = await _db.Specialties.Where(s => dto.SpecialtyIds.Contains(s.Id)).ToListAsync(cancellationToken);
        if (specialties.Count != dto.SpecialtyIds.Distinct().Count())
        {
            return new DoctorMutationResult(DoctorMutationStatus.SpecialtyNotFound);
        }

        doctor.FullName = dto.FullName;
        doctor.Bio = dto.Bio;
        doctor.ConsultationFee = dto.ConsultationFee;
        doctor.Currency = dto.Currency;
        doctor.TimeZoneId = dto.TimeZoneId;
        doctor.YearsOfExperience = dto.YearsOfExperience;
        doctor.ClinicAddress = dto.ClinicAddress;
        doctor.IsAcceptingNewPatients = dto.IsAcceptingNewPatients;
        doctor.UpdatedAtUtc = DateTime.UtcNow;

        doctor.DoctorSpecialties.Clear();
        foreach (var specialty in specialties)
        {
            doctor.DoctorSpecialties.Add(new DoctorSpecialty { DoctorProfileId = doctor.Id, SpecialtyId = specialty.Id, Specialty = specialty });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new DoctorMutationResult(DoctorMutationStatus.Success, ToDto(doctor));
    }

    public async Task<DoctorMutationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doctor is null)
        {
            return new DoctorMutationResult(DoctorMutationStatus.NotFound);
        }

        doctor.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new DoctorMutationResult(DoctorMutationStatus.Success);
    }

    private static DoctorProfileDto ToDto(DoctorProfile d) => new(
        d.Id,
        d.UserId,
        d.FullName,
        d.Bio,
        d.ConsultationFee,
        d.Currency,
        d.TimeZoneId,
        d.YearsOfExperience,
        d.ClinicAddress,
        d.IsAcceptingNewPatients,
        d.DoctorSpecialties.Select(ds => new SpecialtyDto(ds.Specialty.Id, ds.Specialty.Name, ds.Specialty.Description)).ToList());
}
