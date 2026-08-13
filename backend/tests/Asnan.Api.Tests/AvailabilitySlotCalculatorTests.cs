using Asnan.Application.Availability;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;

namespace Asnan.Api.Tests;

/// <summary>
/// Pure unit tests for the slot-derivation algorithm (issue #16) — no
/// database involved, since <see cref="AvailabilitySlotCalculator"/> is a
/// pure function over already-loaded schedule/exception data.
/// </summary>
public class AvailabilitySlotCalculatorTests
{
    private static readonly DateOnly Date = new(2026, 8, 17);

    private static DoctorSchedule Schedule(TimeOnly start, TimeOnly end, int slotMinutes = 30, int bufferMinutes = 0) => new()
    {
        DayOfWeek = Date.DayOfWeek,
        StartTime = start,
        EndTime = end,
        SlotDurationMinutes = slotMinutes,
        BufferMinutes = bufferMinutes,
    };

    [Fact]
    public void Compute_SingleSchedule_NoExceptions_ExpandsIntoEvenlySpacedSlots()
    {
        var schedules = new List<DoctorSchedule> { Schedule(new TimeOnly(9, 0), new TimeOnly(11, 0)) };

        var slots = AvailabilitySlotCalculator.Compute(Date, "Asia/Karachi", 30, schedules, []);

        Assert.Equal(4, slots.Count);
        // Asia/Karachi is UTC+5 year-round (no DST) — 09:00 local == 04:00 UTC.
        Assert.Equal(new DateTime(2026, 8, 17, 4, 0, 0, DateTimeKind.Utc), slots[0].StartUtc);
        Assert.Equal(new DateTime(2026, 8, 17, 4, 30, 0, DateTimeKind.Utc), slots[0].EndUtc);
        Assert.Equal(new DateTime(2026, 8, 17, 5, 30, 0, DateTimeKind.Utc), slots[3].StartUtc);
    }

    [Fact]
    public void Compute_ScheduleWithBuffer_LeavesGapsBetweenSlots()
    {
        var schedules = new List<DoctorSchedule> { Schedule(new TimeOnly(9, 0), new TimeOnly(11, 0), slotMinutes: 30, bufferMinutes: 15) };

        var slots = AvailabilitySlotCalculator.Compute(Date, "Asia/Karachi", 30, schedules, []);

        // 09:00-09:30, 09:45-10:15, 10:30-11:00 — 45-minute step (30 slot + 15 buffer).
        Assert.Equal(3, slots.Count);
        Assert.Equal(TimeSpan.FromMinutes(45), slots[1].StartUtc - slots[0].StartUtc);
    }

    [Fact]
    public void Compute_WholeDayUnavailableException_ReturnsNoSlots()
    {
        var schedules = new List<DoctorSchedule> { Schedule(new TimeOnly(9, 0), new TimeOnly(11, 0)) };
        var exceptions = new List<DoctorAvailabilityException>
        {
            new() { Date = Date, Type = AvailabilityExceptionType.Unavailable, StartTime = null, EndTime = null },
        };

        var slots = AvailabilitySlotCalculator.Compute(Date, "Asia/Karachi", 30, schedules, exceptions);

        Assert.Empty(slots);
    }

    [Fact]
    public void Compute_PartialDayUnavailableException_SplitsScheduleIntoRemainingWindows()
    {
        var schedules = new List<DoctorSchedule> { Schedule(new TimeOnly(9, 0), new TimeOnly(13, 0)) };
        var exceptions = new List<DoctorAvailabilityException>
        {
            new() { Date = Date, Type = AvailabilityExceptionType.Unavailable, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(12, 0) },
        };

        var slots = AvailabilitySlotCalculator.Compute(Date, "Asia/Karachi", 30, schedules, exceptions);

        // 09:00-11:00 (4 slots) + 12:00-13:00 (2 slots) = 6; nothing in the 11:00-12:00 gap.
        Assert.Equal(6, slots.Count);
        Assert.DoesNotContain(slots, s => s.StartUtc == new DateTime(2026, 8, 17, 6, 0, 0, DateTimeKind.Utc)); // 11:00 local
    }

    [Fact]
    public void Compute_ExtraAvailabilityException_UsesDoctorsDefaultSlotDuration()
    {
        var exceptions = new List<DoctorAvailabilityException>
        {
            new() { Date = Date, Type = AvailabilityExceptionType.ExtraAvailability, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0) },
        };

        var slots = AvailabilitySlotCalculator.Compute(Date, "Asia/Karachi", 30, [], exceptions);

        Assert.Equal(2, slots.Count);
        Assert.Equal(new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc), slots[0].StartUtc); // 14:00 local == 09:00 UTC
    }

    [Fact]
    public void Compute_AcrossSpringForwardDstTransition_SkipsInvalidLocalTimeAndConvertsCorrectly()
    {
        // America/New_York, 2026-03-08: clocks spring forward from 01:59:59 to 03:00:00.
        var dstDate = new DateOnly(2026, 3, 8);
        var schedules = new List<DoctorSchedule>
        {
            new()
            {
                DayOfWeek = dstDate.DayOfWeek,
                StartTime = new TimeOnly(1, 0),
                EndTime = new TimeOnly(4, 0),
                SlotDurationMinutes = 30,
                BufferMinutes = 0,
            },
        };

        var slots = AvailabilitySlotCalculator.Compute(dstDate, "America/New_York", 30, schedules, []);

        // 01:00-01:30 valid; 01:30-02:00 is dropped because its end (02:00)
        // is the first invalid instant of the gap; 02:00-02:30 and
        // 02:30-03:00 fall entirely inside the gap; 03:00-03:30 and
        // 03:30-04:00 are valid again once the clock resumes at 03:00.
        Assert.Equal(3, slots.Count);

        // Pre-transition slot converts at EST (UTC-5).
        Assert.Equal(new DateTime(2026, 3, 8, 6, 0, 0, DateTimeKind.Utc), slots[0].StartUtc);
        Assert.Equal(new DateTime(2026, 3, 8, 6, 30, 0, DateTimeKind.Utc), slots[0].EndUtc);

        // Post-transition slots convert at EDT (UTC-4) — a 1-hour local shift
        // (1:00-1:30 vs 3:00-3:30) lands 1 hour apart in UTC too (06:00 vs
        // 07:00), not 2 hours, proving the -5 -> -4 offset change was applied
        // rather than the local clock's 2-hour jump being taken at face value.
        Assert.Equal(new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc), slots[1].StartUtc);
        Assert.Equal(new DateTime(2026, 3, 8, 7, 30, 0, DateTimeKind.Utc), slots[2].StartUtc);
    }

    [Fact]
    public void Compute_ZeroOrNegativeSlotDuration_ReturnsNoSlotsInsteadOfLooping()
    {
        var schedules = new List<DoctorSchedule> { Schedule(new TimeOnly(9, 0), new TimeOnly(11, 0), slotMinutes: 0) };

        var slots = AvailabilitySlotCalculator.Compute(Date, "Asia/Karachi", 30, schedules, []);

        Assert.Empty(slots);
    }
}
