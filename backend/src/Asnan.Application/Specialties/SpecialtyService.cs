using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Specialties;

public class SpecialtyService : ISpecialtyService
{
    private readonly IApplicationDbContext _db;

    public SpecialtyService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SpecialtyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Specialties
            .OrderBy(s => s.Name)
            .Select(s => new SpecialtyDto(s.Id, s.Name, s.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<SpecialtyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Specialties
            .Where(s => s.Id == id)
            .Select(s => new SpecialtyDto(s.Id, s.Name, s.Description))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SpecialtyMutationResult> CreateAsync(CreateSpecialtyDto dto, CancellationToken cancellationToken = default)
    {
        if (await _db.Specialties.AnyAsync(s => s.Name == dto.Name, cancellationToken))
        {
            return new SpecialtyMutationResult(SpecialtyMutationStatus.DuplicateName);
        }

        var specialty = new Specialty { Name = dto.Name, Description = dto.Description };
        _db.Specialties.Add(specialty);
        await _db.SaveChangesAsync(cancellationToken);

        return new SpecialtyMutationResult(SpecialtyMutationStatus.Success, new SpecialtyDto(specialty.Id, specialty.Name, specialty.Description));
    }

    public async Task<SpecialtyMutationResult> UpdateAsync(Guid id, UpdateSpecialtyDto dto, CancellationToken cancellationToken = default)
    {
        var specialty = await _db.Specialties.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (specialty is null)
        {
            return new SpecialtyMutationResult(SpecialtyMutationStatus.NotFound);
        }

        if (await _db.Specialties.AnyAsync(s => s.Id != id && s.Name == dto.Name, cancellationToken))
        {
            return new SpecialtyMutationResult(SpecialtyMutationStatus.DuplicateName);
        }

        specialty.Name = dto.Name;
        specialty.Description = dto.Description;
        specialty.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new SpecialtyMutationResult(SpecialtyMutationStatus.Success, new SpecialtyDto(specialty.Id, specialty.Name, specialty.Description));
    }

    public async Task<SpecialtyMutationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var specialty = await _db.Specialties.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (specialty is null)
        {
            return new SpecialtyMutationResult(SpecialtyMutationStatus.NotFound);
        }

        specialty.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new SpecialtyMutationResult(SpecialtyMutationStatus.Success);
    }
}
