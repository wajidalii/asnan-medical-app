using Asnan.Application.Specialties;
using FluentValidation;

namespace Asnan.Application.Doctors;

/// <summary>
/// "Rating" is deliberately not a sort option — no review/rating system
/// exists yet (that's a future milestone); adding a sort key with nothing
/// behind it would be a fake API surface, not a placeholder.
/// </summary>
public enum DoctorSortBy
{
    Name,
    Fee,
    Experience,
}

/// <summary>
/// Public-facing doctor listing item — deliberately excludes internal-only
/// fields (e.g. <c>UserId</c>, <c>TimeZoneId</c>) that patients browsing the
/// directory don't need and that aren't meant for patient-facing display.
/// </summary>
public record DoctorListItemDto(
    Guid Id,
    string FullName,
    string? Bio,
    decimal ConsultationFee,
    string Currency,
    int? YearsOfExperience,
    string? ClinicAddress,
    bool IsAcceptingNewPatients,
    List<SpecialtyDto> Specialties);

/// <summary>
/// No "available on date X" filter here — that requires the availability
/// model (DoctorSchedules/DoctorAvailabilityExceptions), which doesn't exist
/// until Milestone 4. Flagged rather than faked; see the PR for this issue.
/// </summary>
public record DoctorSearchQuery(
    string? Search = null,
    List<Guid>? SpecialtyIds = null,
    DoctorSortBy SortBy = DoctorSortBy.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20);

public class DoctorSearchQueryValidator : AbstractValidator<DoctorSearchQuery>
{
    public DoctorSearchQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
