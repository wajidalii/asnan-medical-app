using Asnan.Application.Availability;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// REQUIRED per issue #17: fires many simultaneous hold requests for the
/// exact same doctor+slot and asserts exactly one succeeds. This is the
/// concurrency-correctness proof for ARCHITECTURE.md §6's booking critical
/// section — the DB unique index on AppointmentHold.ActiveSlotKey (see its
/// doc comment) is what the assertion is actually exercising, not
/// application-level logic. Same real-MySQL, multi-DbContext pattern as
/// DbConcurrencyTests' refresh-token-rotation race proof.
/// </summary>
[Collection("Database")]
public class AppointmentHoldConcurrencyTests
{
    private readonly DatabaseFixture _fixture;

    public AppointmentHoldConcurrencyTests(DatabaseFixture fixture)
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

    private static IAppointmentHoldService CreateHoldService(AsnanDbContext db) =>
        new AppointmentHoldService(db, new AvailabilityComputationService(db), Options.Create(new HoldOptions { TtlMinutes = 5 }));

    [Fact]
    public async Task CreateAsync_ManyConcurrentRequestsForTheSameSlot_ExactlyOneSucceeds()
    {
        Guid doctorId;
        Guid patientUserId;
        DateTime slotStartUtc;
        DateTime slotEndUtc;

        await using (var seedDb = CreateDb())
        {
            var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            seedDb.Users.AddRange(doctorUser, patientUser);

            var doctor = new DoctorProfile
            {
                UserId = doctorUser.Id,
                FullName = "Dr. Concurrency Test",
                ConsultationFee = 100m,
                Currency = "USD",
                TimeZoneId = "UTC",
            };
            seedDb.DoctorProfiles.Add(doctor);

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
            seedDb.DoctorSchedules.Add(new DoctorSchedule
            {
                DoctorProfile = doctor,
                DayOfWeek = date.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                SlotDurationMinutes = 30,
                BufferMinutes = 0,
            });

            await seedDb.SaveChangesAsync();

            doctorId = doctor.Id;
            patientUserId = patientUser.Id;
            slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
            slotEndUtc = slotStartUtc.AddMinutes(30);
        }

        const int concurrency = 10;
        var contexts = Enumerable.Range(0, concurrency).Select(_ => CreateDb()).ToList();

        try
        {
            var tasks = contexts
                .Select(db => CreateHoldService(db).CreateAsync(patientUserId, new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc)))
                .ToList();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, results.Count(r => r.Status == CreateHoldStatus.Success));
            Assert.Equal(concurrency - 1, results.Count(r => r.Status == CreateHoldStatus.Conflict));
            Assert.DoesNotContain(results, r => r.Status is CreateHoldStatus.DoctorNotFound or CreateHoldStatus.SlotNotAvailable);
        }
        finally
        {
            foreach (var db in contexts)
            {
                await db.DisposeAsync();
            }
        }
    }
}
