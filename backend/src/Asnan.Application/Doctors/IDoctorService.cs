namespace Asnan.Application.Doctors;

public interface IDoctorService
{
    Task<List<DoctorProfileDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DoctorProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><paramref name="adminUserId"/> is only for the audit trail (ARCHITECTURE.md §13's "admin actions") — DoctorsController is already Admin-role-gated, this isn't an authorization check.</summary>
    Task<DoctorMutationResult> CreateAsync(CreateDoctorDto dto, Guid adminUserId, CancellationToken cancellationToken = default);

    Task<DoctorMutationResult> UpdateAsync(Guid id, UpdateDoctorDto dto, Guid adminUserId, CancellationToken cancellationToken = default);

    Task<DoctorMutationResult> DeleteAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default);
}
