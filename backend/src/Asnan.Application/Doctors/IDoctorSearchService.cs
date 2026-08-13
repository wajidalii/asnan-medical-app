using Asnan.Application.Common;

namespace Asnan.Application.Doctors;

public interface IDoctorSearchService
{
    Task<PagedResult<DoctorListItemDto>> SearchAsync(DoctorSearchQuery query, CancellationToken cancellationToken = default);
}
