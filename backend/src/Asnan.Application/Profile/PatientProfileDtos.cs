using Asnan.Domain.Enums;
using FluentValidation;

namespace Asnan.Application.Profile;

/// <summary>Email/Mobile are read-only here — sourced from User (the verified login identity), not editable via this endpoint. See PatientProfile's doc comment.</summary>
public record PatientProfileDto(
    Guid UserId,
    string? Email,
    string? Mobile,
    string FullName,
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? Phone,
    string? AddressLine,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    bool HasPhoto);

public record UpdatePatientProfileDto(
    string FullName,
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? Phone,
    string? AddressLine,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

public class UpdatePatientProfileDtoValidator : AbstractValidator<UpdatePatientProfileDto>
{
    public UpdatePatientProfileDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.AddressLine).MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).MaximumLength(200);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(30);
        RuleFor(x => x.Gender).IsInEnum().When(x => x.Gender.HasValue);
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");
    }
}
