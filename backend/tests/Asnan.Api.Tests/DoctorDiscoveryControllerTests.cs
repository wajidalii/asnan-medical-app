using System.Net;
using System.Net.Http.Json;
using Asnan.Application.Common;
using Asnan.Application.Doctors;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for the public doctor directory (issue #12)
/// — real controller, real routing, real MySQL, no auth (public endpoint).
/// Every test scopes its assertions to doctors it creates itself, using a
/// unique marker embedded in the name/specialty, since the dev database
/// accumulates rows across test runs (same convention as DbConstraintTests).
/// </summary>
[Collection("Database")]
public class DoctorDiscoveryControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public DoctorDiscoveryControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<Specialty> SeedSpecialtyAsync(AsnanDbContext db, string name)
    {
        var specialty = new Specialty { Name = name };
        db.Specialties.Add(specialty);
        await db.SaveChangesAsync();
        return specialty;
    }

    private async Task<DoctorProfile> SeedDoctorAsync(
        AsnanDbContext db, string fullName, decimal fee, int? experience, params Specialty[] specialties)
    {
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Doctor });

        var doctor = new DoctorProfile
        {
            UserId = user.Id,
            FullName = fullName,
            ConsultationFee = fee,
            Currency = "USD",
            TimeZoneId = "UTC",
            YearsOfExperience = experience,
            IsAcceptingNewPatients = true,
        };
        doctor.DoctorSpecialties = specialties
            .Select(s => new DoctorSpecialty { DoctorProfile = doctor, SpecialtyId = s.Id })
            .ToList();

        db.DoctorProfiles.Add(doctor);
        await db.SaveChangesAsync();
        return doctor;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Search_ByDoctorName_ReturnsMatchingDoctor()
    {
        var marker = $"Zephyr{Guid.NewGuid():N}";
        await using (var db = CreateDb())
        {
            await SeedDoctorAsync(db, $"Dr. {marker} Smith", 100m, 5);
            await SeedDoctorAsync(db, "Dr. Unrelated Other", 100m, 5);
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors?search={marker}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Contains(marker, result.Items[0].FullName);
    }

    [Fact]
    public async Task Search_BySpecialtyName_ReturnsDoctorWithThatSpecialty()
    {
        var marker = $"Otorhinolaryngology{Guid.NewGuid():N}";
        await using (var db = CreateDb())
        {
            var specialty = await SeedSpecialtyAsync(db, marker);
            await SeedDoctorAsync(db, "Dr. Specialty Search Test", 100m, 5, specialty);
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors?search={marker}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Contains(result.Items[0].Specialties, s => s.Name == marker);
    }

    [Fact]
    public async Task FilterBySpecialtyId_ExcludesDoctorsWithoutThatSpecialty()
    {
        var marker = $"FilterTest{Guid.NewGuid():N}";
        Guid matchId, otherId, targetSpecialtyId;
        await using (var db = CreateDb())
        {
            var targetSpecialty = await SeedSpecialtyAsync(db, $"{marker}-Target");
            var otherSpecialty = await SeedSpecialtyAsync(db, $"{marker}-Other");
            targetSpecialtyId = targetSpecialty.Id;

            var match = await SeedDoctorAsync(db, $"Dr. {marker} Match", 100m, 5, targetSpecialty);
            var other = await SeedDoctorAsync(db, $"Dr. {marker} NoMatch", 100m, 5, otherSpecialty);
            matchId = match.Id;
            otherId = other.Id;
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors?search={marker}&specialtyIds={targetSpecialtyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();

        Assert.NotNull(result);
        Assert.Contains(result!.Items, d => d.Id == matchId);
        Assert.DoesNotContain(result.Items, d => d.Id == otherId);
    }

    [Fact]
    public async Task SortByFee_OrdersAscendingAndDescending()
    {
        var marker = $"FeeSort{Guid.NewGuid():N}";
        await using (var db = CreateDb())
        {
            await SeedDoctorAsync(db, $"Dr. {marker} A", 300m, 1);
            await SeedDoctorAsync(db, $"Dr. {marker} B", 100m, 1);
            await SeedDoctorAsync(db, $"Dr. {marker} C", 200m, 1);
        }

        var client = CreateClient();

        var ascResponse = await client.GetAsync($"/api/v1/doctors?search={marker}&sortBy=Fee&pageSize=10");
        var asc = await ascResponse.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();
        Assert.NotNull(asc);
        Assert.Equal(3, asc!.Items.Count);
        Assert.Equal(new[] { 100m, 200m, 300m }, asc.Items.Select(d => d.ConsultationFee));

        var descResponse = await client.GetAsync($"/api/v1/doctors?search={marker}&sortBy=Fee&descending=true&pageSize=10");
        var desc = await descResponse.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();
        Assert.NotNull(desc);
        Assert.Equal(new[] { 300m, 200m, 100m }, desc!.Items.Select(d => d.ConsultationFee));
    }

    [Fact]
    public async Task SortByExperience_OrdersAscending()
    {
        var marker = $"ExpSort{Guid.NewGuid():N}";
        await using (var db = CreateDb())
        {
            await SeedDoctorAsync(db, $"Dr. {marker} A", 100m, 10);
            await SeedDoctorAsync(db, $"Dr. {marker} B", 100m, 2);
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors?search={marker}&sortBy=Experience&pageSize=10");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();

        Assert.NotNull(result);
        Assert.Equal(new int?[] { 2, 10 }, result!.Items.Select(d => d.YearsOfExperience));
    }

    [Fact]
    public async Task Pagination_SplitsResultsAcrossPagesWithCorrectTotalCount()
    {
        var marker = $"PageTest{Guid.NewGuid():N}";
        await using (var db = CreateDb())
        {
            for (var i = 0; i < 5; i++)
            {
                await SeedDoctorAsync(db, $"Dr. {marker} {i}", 100m, i);
            }
        }

        var client = CreateClient();

        var page1Response = await client.GetAsync($"/api/v1/doctors?search={marker}&pageSize=2&page=1");
        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();
        Assert.NotNull(page1);
        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);

        var page3Response = await client.GetAsync($"/api/v1/doctors?search={marker}&pageSize=2&page=3");
        var page3 = await page3Response.Content.ReadFromJsonAsync<PagedResult<DoctorListItemDto>>();
        Assert.NotNull(page3);
        Assert.Equal(5, page3!.TotalCount);
        Assert.Single(page3!.Items);
    }

    [Fact]
    public async Task Search_WithInvalidPageSize_ReturnsBadRequest()
    {
        var response = await CreateClient().GetAsync("/api/v1/doctors?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsFullDetailForExistingDoctor()
    {
        var marker = $"DetailTest{Guid.NewGuid():N}";
        Guid doctorId;
        await using (var db = CreateDb())
        {
            var specialty = await SeedSpecialtyAsync(db, $"{marker}-Specialty");
            var doctor = await SeedDoctorAsync(db, $"Dr. {marker}", 175m, 12, specialty);
            doctor.Bio = "About this doctor.";
            doctor.Qualifications = "MBBS, FCPS (Cardiology)";
            doctor.ClinicAddress = "789 Health St";
            doctor.AppointmentDurationMinutes = 45;
            await db.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors/{doctorId}");
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<DoctorDetailDto>();

        Assert.NotNull(detail);
        Assert.Equal(doctorId, detail!.Id);
        Assert.Equal("About this doctor.", detail.Bio);
        Assert.Equal("MBBS, FCPS (Cardiology)", detail.Qualifications);
        Assert.Equal("789 Health St", detail.ClinicAddress);
        Assert.Equal(45, detail.AppointmentDurationMinutes);
        Assert.Equal(175m, detail.ConsultationFee);
        Assert.Equal(12, detail.YearsOfExperience);
        Assert.Contains(detail.Specialties, s => s.Name == $"{marker}-Specialty");
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await CreateClient().GetAsync($"/api/v1/doctors/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ForSoftDeletedDoctor_ReturnsNotFound()
    {
        var marker = $"SoftDeleteTest{Guid.NewGuid():N}";
        Guid doctorId;
        await using (var db = CreateDb())
        {
            var doctor = await SeedDoctorAsync(db, $"Dr. {marker}", 100m, 3);
            doctorId = doctor.Id;

            doctor.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors/{doctorId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_ExcludesInternalOnlyFields()
    {
        var marker = $"FieldTest{Guid.NewGuid():N}";
        await using (var db = CreateDb())
        {
            await SeedDoctorAsync(db, $"Dr. {marker}", 100m, 5);
        }

        var response = await CreateClient().GetAsync($"/api/v1/doctors?search={marker}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timeZoneId", json, StringComparison.OrdinalIgnoreCase);
    }
}
