using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for the doctor schedule / availability
/// exception CRUD API (issue #15) — real controllers, real MySQL, real JWT
/// auth. Covers CRUD, overlap/conflict validation, and the owning-doctor-or-
/// admin object-level authorization requirement.
/// </summary>
[Collection("Database")]
public class DoctorScheduleAndAvailabilityExceptionControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public DoctorScheduleAndAvailabilityExceptionControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid UserId, string Token)> CreateAuthenticatedUserAsync(string roleName)
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);

        var roleId = roleName switch
        {
            "Admin" => RoleIds.Admin,
            "Doctor" => RoleIds.Doctor,
            _ => RoleIds.Patient,
        };
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { roleName });
        return (user.Id, token);
    }

    private async Task<Guid> SeedDoctorProfileAsync(Guid ownerUserId)
    {
        await using var db = CreateDb();
        var doctor = new DoctorProfile
        {
            UserId = ownerUserId,
            FullName = $"Dr. {Guid.NewGuid():N}",
            ConsultationFee = 100m,
            Currency = "USD",
            TimeZoneId = "UTC",
        };
        db.DoctorProfiles.Add(doctor);
        await db.SaveChangesAsync();
        return doctor.Id;
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

    private static CreateDoctorScheduleDto ValidScheduleDto(DayOfWeek day = DayOfWeek.Monday, int startHour = 9, int endHour = 12) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), 30, 5);

    [Fact]
    public async Task Unauthenticated_CannotAccessSchedules()
    {
        var (doctorUserId, _) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);

        var response = await CreateClient().GetAsync($"/api/v1/doctors/{doctorId}/schedules");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OwningDoctor_CanCreateReadUpdateDeleteSchedule()
    {
        var (doctorUserId, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);
        var client = CreateClient(doctorToken);

        var createResponse = await client.PostAsJsonAsync($"/api/v1/doctors/{doctorId}/schedules", ValidScheduleDto());
        Assert.True(createResponse.IsSuccessStatusCode, await createResponse.Content.ReadAsStringAsync());
        var created = await createResponse.Content.ReadFromJsonAsync<DoctorScheduleDto>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/v1/doctors/{doctorId}/schedules");
        getResponse.EnsureSuccessStatusCode();
        var list = await getResponse.Content.ReadFromJsonAsync<List<DoctorScheduleDto>>();
        Assert.Contains(list!, s => s.Id == created!.Id);

        var updateDto = new UpdateDoctorScheduleDto(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(14, 0), 45, 10);
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/doctors/{doctorId}/schedules/{created!.Id}", updateDto);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<DoctorScheduleDto>();
        Assert.Equal(DayOfWeek.Tuesday, updated!.DayOfWeek);
        Assert.Equal(45, updated.SlotDurationMinutes);

        var deleteResponse = await client.DeleteAsync($"/api/v1/doctors/{doctorId}/schedules/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetAsync($"/api/v1/doctors/{doctorId}/schedules");
        var afterDeleteList = await afterDelete.Content.ReadFromJsonAsync<List<DoctorScheduleDto>>();
        Assert.DoesNotContain(afterDeleteList!, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Admin_CanManageAnyDoctorsSchedule()
    {
        var (doctorUserId, _) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");

        var response = await CreateClient(adminToken).PostAsJsonAsync($"/api/v1/doctors/{doctorId}/schedules", ValidScheduleDto());

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DifferentDoctor_CannotManageAnotherDoctorsSchedule()
    {
        var (doctorAUserId, _) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorAId = await SeedDoctorProfileAsync(doctorAUserId);
        var (_, doctorBToken) = await CreateAuthenticatedUserAsync("Doctor");

        var response = await CreateClient(doctorBToken).PostAsJsonAsync($"/api/v1/doctors/{doctorAId}/schedules", ValidScheduleDto());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSchedule_ForUnknownDoctor_ReturnsNotFound()
    {
        var (_, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");

        var response = await CreateClient(doctorToken).PostAsJsonAsync($"/api/v1/doctors/{Guid.NewGuid()}/schedules", ValidScheduleDto());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSchedule_WithOverlappingWindow_ReturnsConflict()
    {
        var (doctorUserId, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);
        var client = CreateClient(doctorToken);

        (await client.PostAsJsonAsync($"/api/v1/doctors/{doctorId}/schedules", ValidScheduleDto(DayOfWeek.Wednesday, 9, 12))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync($"/api/v1/doctors/{doctorId}/schedules", ValidScheduleDto(DayOfWeek.Wednesday, 11, 14));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSchedule_WithStartAfterEnd_ReturnsBadRequest()
    {
        var (doctorUserId, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);

        var dto = new CreateDoctorScheduleDto(DayOfWeek.Thursday, new TimeOnly(14, 0), new TimeOnly(9, 0), 30, 0);
        var response = await CreateClient(doctorToken).PostAsJsonAsync($"/api/v1/doctors/{doctorId}/schedules", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OwningDoctor_CanCreateReadUpdateDeleteAvailabilityException()
    {
        var (doctorUserId, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);
        var client = CreateClient(doctorToken);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var createDto = new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.Unavailable, null, null, "Holiday");
        var createResponse = await client.PostAsJsonAsync($"/api/v1/doctors/{doctorId}/availability-exceptions", createDto);
        Assert.True(createResponse.IsSuccessStatusCode, await createResponse.Content.ReadAsStringAsync());
        var created = await createResponse.Content.ReadFromJsonAsync<AvailabilityExceptionDto>();
        Assert.NotNull(created);
        Assert.Equal(AvailabilityExceptionType.Unavailable, created!.Type);

        var getResponse = await client.GetAsync($"/api/v1/doctors/{doctorId}/availability-exceptions");
        var list = await getResponse.Content.ReadFromJsonAsync<List<AvailabilityExceptionDto>>();
        Assert.Contains(list!, e => e.Id == created.Id);

        var updateDto = new UpdateAvailabilityExceptionDto(date, AvailabilityExceptionType.ExtraAvailability, new TimeOnly(18, 0), new TimeOnly(20, 0), "Extra evening hours");
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/doctors/{doctorId}/availability-exceptions/{created.Id}", updateDto);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<AvailabilityExceptionDto>();
        Assert.Equal(AvailabilityExceptionType.ExtraAvailability, updated!.Type);
        Assert.Equal(new TimeOnly(18, 0), updated.StartTime);

        var deleteResponse = await client.DeleteAsync($"/api/v1/doctors/{doctorId}/availability-exceptions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CreateException_ExtraAvailabilityWithoutTimes_ReturnsBadRequest()
    {
        var (doctorUserId, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(31));

        var dto = new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.ExtraAvailability, null, null, null);
        var response = await CreateClient(doctorToken).PostAsJsonAsync($"/api/v1/doctors/{doctorId}/availability-exceptions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateException_ConflictingWithExistingWholeDayBlock_ReturnsConflict()
    {
        var (doctorUserId, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorId = await SeedDoctorProfileAsync(doctorUserId);
        var client = CreateClient(doctorToken);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(32));

        var wholeDayDto = new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.Unavailable, null, null, "Sick day");
        (await client.PostAsJsonAsync($"/api/v1/doctors/{doctorId}/availability-exceptions", wholeDayDto)).EnsureSuccessStatusCode();

        var secondDto = new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.ExtraAvailability, new TimeOnly(18, 0), new TimeOnly(20, 0), null);
        var response = await client.PostAsJsonAsync($"/api/v1/doctors/{doctorId}/availability-exceptions", secondDto);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DifferentDoctor_CannotManageAnotherDoctorsAvailabilityException()
    {
        var (doctorAUserId, _) = await CreateAuthenticatedUserAsync("Doctor");
        var doctorAId = await SeedDoctorProfileAsync(doctorAUserId);
        var (_, doctorBToken) = await CreateAuthenticatedUserAsync("Doctor");
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(33));

        var dto = new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.Unavailable, null, null, null);
        var response = await CreateClient(doctorBToken).PostAsJsonAsync($"/api/v1/doctors/{doctorAId}/availability-exceptions", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
