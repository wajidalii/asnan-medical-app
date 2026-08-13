using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Application.Availability;

public class AppointmentHoldService : IAppointmentHoldService
{
    private readonly IApplicationDbContext _db;
    private readonly IAvailabilityComputationService _availabilityService;
    private readonly HoldOptions _options;

    public AppointmentHoldService(IApplicationDbContext db, IAvailabilityComputationService availabilityService, IOptions<HoldOptions> options)
    {
        _db = db;
        _availabilityService = availabilityService;
        _options = options.Value;
    }

    public async Task<CreateHoldResult> CreateAsync(Guid patientUserId, CreateHoldDto dto, CancellationToken cancellationToken = default)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == dto.DoctorId, cancellationToken);
        if (doctor is null)
        {
            return new CreateHoldResult(CreateHoldStatus.DoctorNotFound);
        }

        // Re-derive availability server-side — never trust the client's "this
        // slot is free" claim (ARCHITECTURE.md §6). The requested date is
        // resolved in the doctor's own timezone since that's what the
        // schedule/exception lookup keys off, and a slot near local midnight
        // can fall on a different UTC calendar date.
        var doctorTimeZone = TimeZoneInfo.FindSystemTimeZoneById(doctor.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(dto.SlotStartUtc, doctorTimeZone));

        var availability = await _availabilityService.GetAvailabilityAsync(dto.DoctorId, localDate, cancellationToken);
        var slotIsCurrentlyAvailable = availability.Availability!.Slots.Any(s => s.StartUtc == dto.SlotStartUtc && s.EndUtc == dto.SlotEndUtc);
        if (!slotIsCurrentlyAvailable)
        {
            return new CreateHoldResult(CreateHoldStatus.SlotNotAvailable);
        }

        var now = DateTime.UtcNow;

        // Lazily expire a stale Active hold blocking this exact slot before
        // attempting to claim it. This is purely a UX nicety — the unique
        // index on ActiveSlotKey is what actually guarantees correctness
        // under concurrent requests, not this step.
        var staleHolds = await _db.AppointmentHolds
            .Where(h => h.DoctorProfileId == dto.DoctorId && h.SlotStartUtc == dto.SlotStartUtc && h.Status == HoldStatus.Active && h.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
        if (staleHolds.Count > 0)
        {
            foreach (var stale in staleHolds)
            {
                stale.Status = HoldStatus.Expired;
                stale.ActiveSlotKey = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var rawToken = OpaqueTokenGenerator.Generate();
        var hold = new AppointmentHold
        {
            DoctorProfileId = dto.DoctorId,
            PatientUserId = patientUserId,
            SlotStartUtc = dto.SlotStartUtc,
            SlotEndUtc = dto.SlotEndUtc,
            Status = HoldStatus.Active,
            ExpiresAtUtc = now.AddMinutes(_options.TtlMinutes),
            HoldTokenHash = OpaqueTokenGenerator.Hash(rawToken),
            ActiveSlotKey = $"{dto.DoctorId}|{dto.SlotStartUtc:O}",
        };
        _db.AppointmentHolds.Add(hold);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Someone else's insert for the same (doctor, slot) won the race
            // — see AppointmentHold.ActiveSlotKey's doc comment for why this
            // is the actual correctness guarantee, not the availability
            // check above.
            return new CreateHoldResult(CreateHoldStatus.Conflict);
        }

        return new CreateHoldResult(
            CreateHoldStatus.Success,
            new HoldDto(hold.Id, hold.DoctorProfileId, hold.SlotStartUtc, hold.SlotEndUtc, rawToken, hold.ExpiresAtUtc));
    }
}
