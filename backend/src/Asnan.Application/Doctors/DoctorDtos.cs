using Asnan.Application.Specialties;
using FluentValidation;

namespace Asnan.Application.Doctors;

public record DoctorProfileDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string? Bio,
    decimal ConsultationFee,
    string Currency,
    string TimeZoneId,
    int? YearsOfExperience,
    string? ClinicAddress,
    bool IsAcceptingNewPatients,
    List<SpecialtyDto> Specialties);

public record CreateDoctorDto(
    Guid UserId,
    string FullName,
    string? Bio,
    decimal ConsultationFee,
    string Currency,
    string TimeZoneId,
    int? YearsOfExperience,
    string? ClinicAddress,
    bool IsAcceptingNewPatients,
    List<Guid> SpecialtyIds);

public record UpdateDoctorDto(
    string FullName,
    string? Bio,
    decimal ConsultationFee,
    string Currency,
    string TimeZoneId,
    int? YearsOfExperience,
    string? ClinicAddress,
    bool IsAcceptingNewPatients,
    List<Guid> SpecialtyIds);

public class CreateDoctorDtoValidator : AbstractValidator<CreateDoctorDto>
{
    public CreateDoctorDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.ConsultationFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0).When(x => x.YearsOfExperience.HasValue);
        RuleFor(x => x.ClinicAddress).MaximumLength(512);
        RuleFor(x => x.SpecialtyIds).NotEmpty().WithMessage("At least one specialty is required.");
    }
}

public class UpdateDoctorDtoValidator : AbstractValidator<UpdateDoctorDto>
{
    public UpdateDoctorDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.ConsultationFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.YearsOfExperience).GreaterThanOrEqualTo(0).When(x => x.YearsOfExperience.HasValue);
        RuleFor(x => x.ClinicAddress).MaximumLength(512);
        RuleFor(x => x.SpecialtyIds).NotEmpty().WithMessage("At least one specialty is required.");
    }
}
