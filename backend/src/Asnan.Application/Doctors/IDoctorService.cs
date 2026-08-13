namespace Asnan.Application.Doctors;

public interface IDoctorService
{
    Task<List<DoctorProfileDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DoctorProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DoctorMutationResult> CreateAsync(CreateDoctorDto dto, CancellationToken cancellationToken = default);

    Task<DoctorMutationResult> UpdateAsync(Guid id, UpdateDoctorDto dto, CancellationToken cancellationToken = default);

    Task<DoctorMutationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
