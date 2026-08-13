using Asnan.Application.Common;
using Asnan.Application.Specialties;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Application.Doctors;

public class DoctorSearchService : IDoctorSearchService
{
    private readonly IApplicationDbContext _db;

    public DoctorSearchService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<DoctorListItemDto>> SearchAsync(DoctorSearchQuery query, CancellationToken cancellationToken = default)
    {
        var doctorsQuery = _db.DoctorProfiles
            .Include(d => d.DoctorSpecialties).ThenInclude(ds => ds.Specialty)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            doctorsQuery = doctorsQuery.Where(d =>
                EF.Functions.Like(d.FullName, pattern) ||
                d.DoctorSpecialties.Any(ds => EF.Functions.Like(ds.Specialty.Name, pattern)));
        }

        if (query.SpecialtyIds is { Count: > 0 })
        {
            doctorsQuery = doctorsQuery.Where(d => d.DoctorSpecialties.Any(ds => query.SpecialtyIds.Contains(ds.SpecialtyId)));
        }

        doctorsQuery = query.SortBy switch
        {
            DoctorSortBy.Fee => query.Descending
                ? doctorsQuery.OrderByDescending(d => d.ConsultationFee)
                : doctorsQuery.OrderBy(d => d.ConsultationFee),
            DoctorSortBy.Experience => query.Descending
                ? doctorsQuery.OrderByDescending(d => d.YearsOfExperience)
                : doctorsQuery.OrderBy(d => d.YearsOfExperience),
            _ => query.Descending
                ? doctorsQuery.OrderByDescending(d => d.FullName)
                : doctorsQuery.OrderBy(d => d.FullName),
        };

        var totalCount = await doctorsQuery.CountAsync(cancellationToken);

        var doctors = await doctorsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DoctorListItemDto>(doctors.Select(ToDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    private static DoctorListItemDto ToDto(DoctorProfile d) => new(
        d.Id,
        d.FullName,
        d.Bio,
        d.ConsultationFee,
        d.Currency,
        d.YearsOfExperience,
        d.ClinicAddress,
        d.IsAcceptingNewPatients,
        d.DoctorSpecialties.Select(ds => new SpecialtyDto(ds.Specialty.Id, ds.Specialty.Name, ds.Specialty.Description)).ToList());
}
