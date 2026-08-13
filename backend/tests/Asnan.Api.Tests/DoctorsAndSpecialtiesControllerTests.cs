using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asnan.Application.Auth;
using Asnan.Application.Doctors;
using Asnan.Application.Specialties;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for the admin-only doctor/specialty CRUD API
/// (issue #11) — real controllers, real routing, real JWT auth, real MySQL.
/// Covers both the CRUD happy paths and the object-level authorization
/// requirement (non-admin access rejected).
/// </summary>
[Collection("Database")]
public class DoctorsAndSpecialtiesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public DoctorsAndSpecialtiesControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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
    public async Task Unauthenticated_CannotListDoctors()
    {
        var response = await CreateClient().GetAsync("/api/v1/admin/doctors");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotCreateSpecialty()
    {
        var (_, patientToken) = await CreateAuthenticatedUserAsync("Patient");

        var response = await CreateClient(patientToken)
            .PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto("Cardiology", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotCreateDoctor()
    {
        var (_, doctorToken) = await CreateAuthenticatedUserAsync("Doctor");

        var dto = new CreateDoctorDto(Guid.NewGuid(), "Dr. Nobody", null, 10m, "USD", "UTC", null, null, true, new List<Guid> { Guid.NewGuid() });

        var response = await CreateClient(doctorToken).PostAsJsonAsync("/api/v1/admin/doctors", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateReadUpdateDeleteSpecialty()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var client = CreateClient(adminToken);

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto($"Specialty-{Guid.NewGuid()}", "desc"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SpecialtyDto>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/v1/admin/specialties/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/admin/specialties/{created.Id}",
            new UpdateSpecialtyDto($"{created.Name}-updated", "new desc"));
        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/v1/admin/specialties/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDelete = await client.GetAsync($"/api/v1/admin/specialties/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task CreateSpecialty_WithDuplicateName_ReturnsConflict()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var client = CreateClient(adminToken);
        var name = $"Specialty-{Guid.NewGuid()}";

        (await client.PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto(name, null))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto(name, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateReadUpdateDeleteDoctor_AndPromotesUserToDoctorRole()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var (targetUserId, _) = await CreateAuthenticatedUserAsync("Patient");
        var client = CreateClient(adminToken);

        var specialtyResponse = await client.PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto($"Specialty-{Guid.NewGuid()}", null));
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<SpecialtyDto>();

        var createDoctorDto = new CreateDoctorDto(
            targetUserId, "Dr. Jane Test", "Bio", 100m, "USD", "Asia/Karachi", 5, "123 Clinic Rd", true,
            new List<Guid> { specialty!.Id });

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/doctors", createDoctorDto);
        Assert.True(createResponse.IsSuccessStatusCode, await createResponse.Content.ReadAsStringAsync());
        var created = await createResponse.Content.ReadFromJsonAsync<DoctorProfileDto>();
        Assert.NotNull(created);
        Assert.Single(created!.Specialties);

        // The admin-CRUD API is how an existing Patient-role user is promoted
        // to Doctor — no parallel doctor-auth flow exists (ARCHITECTURE.md §2.2).
        await using (var db = CreateDb())
        {
            var hasDoctorRole = await db.UserRoles.AnyAsync(ur => ur.UserId == targetUserId && ur.RoleId == RoleIds.Doctor);
            Assert.True(hasDoctorRole);
        }

        var getResponse = await client.GetAsync($"/api/v1/admin/doctors/{created.Id}");
        getResponse.EnsureSuccessStatusCode();

        var updateDto = new UpdateDoctorDto("Dr. Jane Updated", "Updated bio", 150m, "USD", "Asia/Karachi", 6, "456 New Rd", false, new List<Guid> { specialty.Id });
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/doctors/{created.Id}", updateDto);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<DoctorProfileDto>();
        Assert.Equal("Dr. Jane Updated", updated!.FullName);
        Assert.False(updated.IsAcceptingNewPatients);

        var deleteResponse = await client.DeleteAsync($"/api/v1/admin/doctors/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDelete = await client.GetAsync($"/api/v1/admin/doctors/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task CreateDoctor_WithUnknownUserId_ReturnsNotFound()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var client = CreateClient(adminToken);

        var specialtyResponse = await client.PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto($"Specialty-{Guid.NewGuid()}", null));
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<SpecialtyDto>();

        var dto = new CreateDoctorDto(Guid.NewGuid(), "Ghost Doctor", null, 50m, "USD", "UTC", null, null, true, new List<Guid> { specialty!.Id });

        var response = await client.PostAsJsonAsync("/api/v1/admin/doctors", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateDoctor_WithUnknownSpecialty_ReturnsBadRequest()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var (targetUserId, _) = await CreateAuthenticatedUserAsync("Patient");
        var client = CreateClient(adminToken);

        var dto = new CreateDoctorDto(targetUserId, "Dr. No Specialty", null, 50m, "USD", "UTC", null, null, true, new List<Guid> { Guid.NewGuid() });

        var response = await client.PostAsJsonAsync("/api/v1/admin/doctors", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDoctor_ForUserWithExistingProfile_ReturnsConflict()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var (targetUserId, _) = await CreateAuthenticatedUserAsync("Patient");
        var client = CreateClient(adminToken);

        var specialtyResponse = await client.PostAsJsonAsync("/api/v1/admin/specialties", new CreateSpecialtyDto($"Specialty-{Guid.NewGuid()}", null));
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<SpecialtyDto>();

        var dto = new CreateDoctorDto(targetUserId, "Dr. Duplicate", null, 50m, "USD", "UTC", null, null, true, new List<Guid> { specialty!.Id });
        (await client.PostAsJsonAsync("/api/v1/admin/doctors", dto)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/admin/doctors", dto);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateDoctor_WithEmptySpecialtyList_ReturnsBadRequest()
    {
        var (_, adminToken) = await CreateAuthenticatedUserAsync("Admin");
        var (targetUserId, _) = await CreateAuthenticatedUserAsync("Patient");
        var client = CreateClient(adminToken);

        var dto = new CreateDoctorDto(targetUserId, "Dr. Empty", null, 50m, "USD", "UTC", null, null, true, new List<Guid>());

        var response = await client.PostAsJsonAsync("/api/v1/admin/doctors", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
