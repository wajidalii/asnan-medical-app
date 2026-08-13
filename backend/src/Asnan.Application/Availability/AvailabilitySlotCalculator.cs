using Asnan.Domain.Entities;
using Asnan.Domain.Enums;

namespace Asnan.Application.Availability;

/// <summary>
/// Pure slot-derivation logic (ARCHITECTURE.md §6) — no I/O, so it's directly
/// unit-testable without a database. Expands a doctor's recurring schedule
/// for the requested date, applies availability-exception overrides, and
/// converts each slot to UTC with the doctor's timezone applied at the exact
/// slot instant (DST-correct across a transition date).
///
/// Does NOT subtract active holds or booked appointments itself — that's
/// <see cref="AvailabilityComputationService"/>'s job, layered on top of this
/// pure result. Booked Appointments still can't be subtracted anywhere; that
/// table doesn't exist until Milestone 6. Flagged rather than faked.
/// </summary>
public static class AvailabilitySlotCalculator
{
    public static List<AvailabilitySlotDto> Compute(
        DateOnly date,
        string timeZoneId,
        int defaultSlotDurationMinutes,
        IReadOnlyList<DoctorSchedule> schedules,
        IReadOnlyList<DoctorAvailabilityException> exceptions)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        // A whole-day Unavailable block (no times) wipes the entire date.
        if (exceptions.Any(e => e.Type == AvailabilityExceptionType.Unavailable && e.StartTime is null && e.EndTime is null))
        {
            return [];
        }

        var unavailableWindows = exceptions
            .Where(e => e.Type == AvailabilityExceptionType.Unavailable && e.StartTime is not null && e.EndTime is not null)
            .Select(e => (Start: e.StartTime!.Value, End: e.EndTime!.Value))
            .ToList();

        var slots = new List<AvailabilitySlotDto>();

        foreach (var schedule in schedules.Where(s => s.DayOfWeek == date.DayOfWeek))
        {
            foreach (var (windowStart, windowEnd) in Subtract(schedule.StartTime, schedule.EndTime, unavailableWindows))
            {
                slots.AddRange(GenerateSlots(date, timeZone, windowStart, windowEnd, schedule.SlotDurationMinutes, schedule.BufferMinutes));
            }
        }

        foreach (var exception in exceptions.Where(e => e.Type == AvailabilityExceptionType.ExtraAvailability && e.StartTime is not null && e.EndTime is not null))
        {
            slots.AddRange(GenerateSlots(date, timeZone, exception.StartTime!.Value, exception.EndTime!.Value, defaultSlotDurationMinutes, 0));
        }

        return slots.OrderBy(s => s.StartUtc).ToList();
    }

    /// <summary>Interval subtraction: removes each of <paramref name="subtract"/> from [start, end).</summary>
    private static List<(TimeOnly Start, TimeOnly End)> Subtract(TimeOnly start, TimeOnly end, List<(TimeOnly Start, TimeOnly End)> subtract)
    {
        var remaining = new List<(TimeOnly Start, TimeOnly End)> { (start, end) };

        foreach (var (subStart, subEnd) in subtract)
        {
            var next = new List<(TimeOnly, TimeOnly)>();
            foreach (var (rStart, rEnd) in remaining)
            {
                if (subEnd <= rStart || subStart >= rEnd)
                {
                    next.Add((rStart, rEnd));
                    continue;
                }

                if (subStart > rStart)
                {
                    next.Add((rStart, subStart));
                }

                if (subEnd < rEnd)
                {
                    next.Add((subEnd, rEnd));
                }
            }

            remaining = next;
        }

        return remaining;
    }

    private static IEnumerable<AvailabilitySlotDto> GenerateSlots(
        DateOnly date, TimeZoneInfo timeZone, TimeOnly windowStart, TimeOnly windowEnd, int slotDurationMinutes, int bufferMinutes)
    {
        if (slotDurationMinutes <= 0)
        {
            yield break;
        }

        var cursor = windowStart;
        while (true)
        {
            var slotEnd = cursor.Add(TimeSpan.FromMinutes(slotDurationMinutes));

            // TimeOnly arithmetic wraps past midnight — schedules don't cross
            // midnight (enforced at creation), so a wrap means we've run off
            // the end of this calendar day's window.
            if (slotEnd < cursor || slotEnd > windowEnd)
            {
                yield break;
            }

            var localStart = date.ToDateTime(cursor, DateTimeKind.Unspecified);
            var localEnd = date.ToDateTime(slotEnd, DateTimeKind.Unspecified);

            // Skip slots that fall in a spring-forward DST gap — that local
            // time never occurred, so there's nothing to convert or offer.
            if (!timeZone.IsInvalidTime(localStart) && !timeZone.IsInvalidTime(localEnd))
            {
                yield return new AvailabilitySlotDto(
                    TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
                    TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
            }

            var next = cursor.Add(TimeSpan.FromMinutes(slotDurationMinutes + bufferMinutes));
            if (next <= cursor)
            {
                yield break;
            }

            cursor = next;
        }
    }
}
