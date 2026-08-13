using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for POST /api/v1/appointments/holds (issue
/// #17) — real controller, real MySQL, real JWT auth. The concurrency
/// correctness proof lives separately in AppointmentHoldConcurrencyTests.
/// </summary>
[Collection("Database")]
public class AppointmentHoldsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public AppointmentHoldsControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid DoctorId, DateTime SlotStartUtc, DateTime SlotEndUtc)> SeedDoctorWithScheduleAsync()
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(doctorUser);

        var doctor = new DoctorProfile
        {
            UserId = doctorUser.Id,
            FullName = "Dr. Hold Test",
            ConsultationFee = 100m,
            Currency = "USD",
            TimeZoneId = "UTC",
        };
        db.DoctorProfiles.Add(doctor);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25));
        db.DoctorSchedules.Add(new DoctorSchedule
        {
            DoctorProfile = doctor,
            DayOfWeek = date.DayOfWeek,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            SlotDurationMinutes = 30,
            BufferMinutes = 0,
        });

        await db.SaveChangesAsync();

        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        return (doctor.Id, slotStartUtc, slotStartUtc.AddMinutes(30));
    }

    private async Task<string> CreatePatientTokenAsync()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { "Patient" });
        return token;
    }

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient();
        if (bearerToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();

        var response = await CreateClient().PostAsJsonAsync("/api/v1/appointments/holds", new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForRealAvailableSlot_ReturnsCreatedWithHoldToken()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var token = await CreatePatientTokenAsync();

        var response = await CreateClient(token).PostAsJsonAsync("/api/v1/appointments/holds", new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hold = await response.Content.ReadFromJsonAsync<HoldDto>();
        Assert.NotNull(hold);
        Assert.False(string.IsNullOrWhiteSpace(hold!.HoldToken));
        Assert.Equal(slotStartUtc, hold.SlotStartUtc);
    }

    [Fact]
    public async Task Create_ForUnknownDoctor_ReturnsNotFound()
    {
        var token = await CreatePatientTokenAsync();
        var dto = new CreateHoldDto(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddMinutes(30));

        var response = await CreateClient(token).PostAsJsonAsync("/api/v1/appointments/holds", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForSlotThatDoesNotMatchTheSchedule_ReturnsConflict()
    {
        var (doctorId, _, _) = await SeedDoctorWithScheduleAsync();
        var token = await CreatePatientTokenAsync();

        // 03:00 UTC isn't within the seeded 09:00-10:00 schedule window.
        var badStart = DateTime.UtcNow.AddDays(25).Date.AddHours(3);
        var dto = new CreateHoldDto(doctorId, badStart, badStart.AddMinutes(30));

        var response = await CreateClient(token).PostAsJsonAsync("/api/v1/appointments/holds", dto);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForAlreadyHeldSlot_ReturnsConflict()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var token = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var dto = new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc);

        var first = await client.PostAsJsonAsync("/api/v1/appointments/holds", dto);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/appointments/holds", dto);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_SuccessfulHold_ExcludesSlotFromSubsequentAvailabilityRead()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var token = await CreatePatientTokenAsync();
        var client = CreateClient(token);

        (await client.PostAsJsonAsync("/api/v1/appointments/holds", new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc)))
            .EnsureSuccessStatusCode();

        var date = DateOnly.FromDateTime(slotStartUtc);
        var availabilityResponse = await client.GetAsync($"/api/v1/availability/doctors/{doctorId}?date={date:yyyy-MM-dd}");
        availabilityResponse.EnsureSuccessStatusCode();
        var availability = await availabilityResponse.Content.ReadFromJsonAsync<DoctorAvailabilityDto>();

        Assert.NotNull(availability);
        Assert.DoesNotContain(availability!.Slots, s => s.StartUtc == slotStartUtc);
    }
}
