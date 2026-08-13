using System.Net;
using System.Net.Http.Json;
using Asnan.Application.Availability;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration test for the public availability endpoint (issue
/// #16) against real seeded schedule data in real MySQL — the pure-logic
/// cases live in <see cref="AvailabilitySlotCalculatorTests"/>.
/// </summary>
[Collection("Database")]
public class AvailabilityControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public AvailabilityControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
    {
        _dbFixture = dbFixture;
        _factory = factory;
    }

    private AsnanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(_dbFixture.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new AsnanDbContext(options);
    }

    [Fact]
    public async Task GetAvailability_Unauthenticated_ReturnsComputedSlotsForSeededSchedule()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        Guid doctorId;

        await using (var db = CreateDb())
        {
            var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Doctor });

            var doctor = new DoctorProfile
            {
                UserId = user.Id,
                FullName = "Dr. Availability Test",
                ConsultationFee = 100m,
                Currency = "USD",
                TimeZoneId = "Asia/Karachi",
            };
            db.DoctorProfiles.Add(doctor);

            var schedule = new DoctorSchedule
            {
                DoctorProfile = doctor,
                DayOfWeek = date.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(11, 0),
                SlotDurationMinutes = 30,
                BufferMinutes = 0,
            };
            db.DoctorSchedules.Add(schedule);

            await db.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        var response = await _factory.CreateClient().GetAsync($"/api/v1/availability/doctors/{doctorId}?date={date:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();
        var availability = await response.Content.ReadFromJsonAsync<DoctorAvailabilityDto>();

        Assert.NotNull(availability);
        Assert.Equal("Asia/Karachi", availability!.TimeZoneId);
        Assert.Equal(4, availability.Slots.Count);
        Assert.Equal(new DateTime(date.Year, date.Month, date.Day, 4, 0, 0, DateTimeKind.Utc), availability.Slots[0].StartUtc);
    }

    [Fact]
    public async Task GetAvailability_AppliesWholeDayException_ReturnsNoSlots()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));
        Guid doctorId;

        await using (var db = CreateDb())
        {
            var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            db.Users.Add(user);

            var doctor = new DoctorProfile
            {
                UserId = user.Id,
                FullName = "Dr. Exception Test",
                ConsultationFee = 100m,
                Currency = "USD",
                TimeZoneId = "Asia/Karachi",
            };
            db.DoctorProfiles.Add(doctor);
            db.DoctorSchedules.Add(new DoctorSchedule
            {
                DoctorProfile = doctor,
                DayOfWeek = date.DayOfWeek,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(11, 0),
                SlotDurationMinutes = 30,
            });
            db.DoctorAvailabilityExceptions.Add(new DoctorAvailabilityException
            {
                DoctorProfile = doctor,
                Date = date,
                Type = AvailabilityExceptionType.Unavailable,
                Reason = "Holiday",
            });

            await db.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        var response = await _factory.CreateClient().GetAsync($"/api/v1/availability/doctors/{doctorId}?date={date:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();
        var availability = await response.Content.ReadFromJsonAsync<DoctorAvailabilityDto>();

        Assert.NotNull(availability);
        Assert.Empty(availability!.Slots);
    }

    [Fact]
    public async Task GetAvailability_ForUnknownDoctor_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/availability/doctors/{Guid.NewGuid()}?date=2026-09-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
