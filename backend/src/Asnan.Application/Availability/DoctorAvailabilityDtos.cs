namespace Asnan.Application.Availability;

public record AvailabilitySlotDto(DateTime StartUtc, DateTime EndUtc);

public record DoctorAvailabilityDto(Guid DoctorId, string TimeZoneId, DateOnly Date, List<AvailabilitySlotDto> Slots);
