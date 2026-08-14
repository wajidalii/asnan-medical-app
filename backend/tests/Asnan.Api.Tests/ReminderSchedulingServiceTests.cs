using Asnan.Application.Reminders;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// Direct-service tests for <see cref="ReminderSchedulingService"/> (issue
/// #25) against real MySQL — same style as AppointmentHoldConcurrencyTests.
/// Covers the issue's testing requirement: offset calculation,
/// duplicate-prevention, and suppression on cancellation.
///
/// Offsets are cumulative, not tiered like the cancellation-policy percentage:
/// being 10 minutes out means every configured offset (24h/1h/15min) has
/// already elapsed, so all three fire in one catch-up scan — that's correct
/// "better late than never" behavior, not a bug. Tests that want exactly one
/// reminder to fire use a single-offset options list to avoid that stacking
/// rather than fighting it with carefully-timed multi-offset scenarios.
///
/// All assertions are scoped to each test's own seeded appointment(s) via
/// AppointmentId — other tests in this class share the same real database
/// and seed near-term appointments too (the offsets are all &lt;=24h), so
/// nothing here relies on the scan's aggregate return value.
/// </summary>
[Collection("Database")]
public class ReminderSchedulingServiceTests
{
    private readonly DatabaseFixture _fixture;

    public ReminderSchedulingServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private AsnanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(_fixture.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new AsnanDbContext(options);
    }

    private class FakeReminderSender : IReminderSender
    {
        public bool ThrowOnNextSend { get; set; }

        public Task SendAsync(Appointment appointment, int offsetMinutes, CancellationToken cancellationToken = default)
        {
            if (ThrowOnNextSend)
            {
                ThrowOnNextSend = false;
                throw new InvalidOperationException("Simulated delivery failure.");
            }

            return Task.CompletedTask;
        }
    }

    private static ReminderOptions OptionsWith(params int[] offsetsMinutes) => new() { OffsetsMinutes = [.. offsetsMinutes] };

    private async Task<Appointment> SeedAppointmentAsync(AsnanDbContext db, DateTime slotStartUtc, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.AddRange(doctorUser, patientUser);

        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Reminder Test", ConsultationFee = 50m, Currency = "USD", TimeZoneId = "UTC" };
        db.DoctorProfiles.Add(doctor);

        var appointment = new Appointment
        {
            DoctorProfileId = doctor.Id,
            PatientUserId = patientUser.Id,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddMinutes(30),
            Status = status,
            ConsultationFee = 50m,
            Currency = "USD",
            SourceHoldId = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);

        await db.SaveChangesAsync();
        return appointment;
    }

    [Fact]
    public async Task ScanAndSendDueReminders_AppointmentWithinTheOffsetWindow_SendsAndRecordsIt()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        var appointment = await SeedAppointmentAsync(db, now.AddMinutes(50)); // within a 60-minute offset

        var service = new ReminderSchedulingService(db, new FakeReminderSender(), Options.Create(OptionsWith(60)));
        await service.ScanAndSendDueRemindersAsync(now);

        var reminder = await db.Reminders.SingleAsync(r => r.AppointmentId == appointment.Id);
        Assert.Equal(60, reminder.OffsetMinutes);
        Assert.Equal(ReminderStatus.Sent, reminder.Status);
        Assert.NotNull(reminder.SentAtUtc);
    }

    [Fact]
    public async Task ScanAndSendDueReminders_AppointmentOutsideTheOffsetWindow_CreatesNoReminderForIt()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        var appointment = await SeedAppointmentAsync(db, now.AddDays(10)); // far outside a 60-minute offset

        var service = new ReminderSchedulingService(db, new FakeReminderSender(), Options.Create(OptionsWith(60)));
        await service.ScanAndSendDueRemindersAsync(now);

        Assert.False(await db.Reminders.AnyAsync(r => r.AppointmentId == appointment.Id));
    }

    [Fact]
    public async Task ScanAndSendDueReminders_RunTwiceForTheSameDueAppointment_DoesNotCreateAnotherReminder()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        var appointment = await SeedAppointmentAsync(db, now.AddMinutes(10)); // within a 15-minute offset

        var service = new ReminderSchedulingService(db, new FakeReminderSender(), Options.Create(OptionsWith(15)));
        await service.ScanAndSendDueRemindersAsync(now);
        await service.ScanAndSendDueRemindersAsync(now.AddSeconds(30));

        var reminders = await db.Reminders.Where(r => r.AppointmentId == appointment.Id).ToListAsync();
        Assert.Single(reminders);
        Assert.Equal(ReminderStatus.Sent, reminders[0].Status);
    }

    [Fact]
    public async Task ScanAndSendDueReminders_CancelledAppointment_NeverGetsAReminder()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        var appointment = await SeedAppointmentAsync(db, now.AddMinutes(10), AppointmentStatus.CancelledByPatient);

        var service = new ReminderSchedulingService(db, new FakeReminderSender(), Options.Create(OptionsWith(1440, 60, 15)));
        await service.ScanAndSendDueRemindersAsync(now);

        Assert.False(await db.Reminders.AnyAsync(r => r.AppointmentId == appointment.Id));
    }

    [Fact]
    public async Task ScanAndSendDueReminders_OffsetsStack_AnAppointmentDueForMultipleOffsetsGetsOneReminderPerOffset()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        // 10 minutes out means every configured offset has elapsed — 24h, 1h, and 15min all fire (catch-up), not just the smallest.
        var appointment = await SeedAppointmentAsync(db, now.AddMinutes(10));

        var service = new ReminderSchedulingService(db, new FakeReminderSender(), Options.Create(OptionsWith(1440, 60, 15)));
        await service.ScanAndSendDueRemindersAsync(now);

        var offsetsSent = await db.Reminders.Where(r => r.AppointmentId == appointment.Id).Select(r => r.OffsetMinutes).ToListAsync();
        Assert.Equal(new[] { 1440, 60, 15 }, offsetsSent.OrderDescending());
    }

    [Fact]
    public async Task ScanAndSendDueReminders_TwoAppointmentsAtDifferentDistances_EachGetsExactlyTheOffsetsItQualifiesFor()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        var dayOut = await SeedAppointmentAsync(db, now.AddHours(23)); // qualifies for the 24h offset only (not 1h/15min — 23h exceeds both)
        var hourOut = await SeedAppointmentAsync(db, now.AddMinutes(45)); // qualifies for 24h and 1h (not 15min — 45min exceeds it)

        var service = new ReminderSchedulingService(db, new FakeReminderSender(), Options.Create(OptionsWith(1440, 60, 15)));
        await service.ScanAndSendDueRemindersAsync(now);

        var dayOutOffsets = await db.Reminders.Where(r => r.AppointmentId == dayOut.Id).Select(r => r.OffsetMinutes).ToListAsync();
        var hourOutOffsets = await db.Reminders.Where(r => r.AppointmentId == hourOut.Id).Select(r => r.OffsetMinutes).ToListAsync();

        Assert.Equal([1440], dayOutOffsets);
        Assert.Equal(new[] { 1440, 60 }, hourOutOffsets.OrderDescending());
    }

    [Fact]
    public async Task ScanAndSendDueReminders_SenderFailure_LeavesTheReminderPendingForRetry()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        var appointment = await SeedAppointmentAsync(db, now.AddMinutes(10));

        var sender = new FakeReminderSender { ThrowOnNextSend = true };
        var service = new ReminderSchedulingService(db, sender, Options.Create(OptionsWith(15)));

        await service.ScanAndSendDueRemindersAsync(now);
        var pending = await db.Reminders.SingleAsync(r => r.AppointmentId == appointment.Id);
        Assert.Equal(ReminderStatus.Pending, pending.Status);

        await service.ScanAndSendDueRemindersAsync(now.AddSeconds(10));
        var sent = await db.Reminders.SingleAsync(r => r.AppointmentId == appointment.Id);
        Assert.Equal(ReminderStatus.Sent, sent.Status);
    }
}
