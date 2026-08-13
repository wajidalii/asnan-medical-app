namespace Asnan.Application.Specialties;

public interface ISpecialtyService
{
    Task<List<SpecialtyDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SpecialtyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SpecialtyMutationResult> CreateAsync(CreateSpecialtyDto dto, CancellationToken cancellationToken = default);

    Task<SpecialtyMutationResult> UpdateAsync(Guid id, UpdateSpecialtyDto dto, CancellationToken cancellationToken = default);

    Task<SpecialtyMutationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
