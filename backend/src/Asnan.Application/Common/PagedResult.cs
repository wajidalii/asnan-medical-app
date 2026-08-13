namespace Asnan.Application.Common;

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);
