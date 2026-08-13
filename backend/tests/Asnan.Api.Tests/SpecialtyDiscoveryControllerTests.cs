using System.Net.Http.Json;
using Asnan.Application.Specialties;
using Asnan.Domain.Entities;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration test for the public specialty list (companion
/// endpoint added for #14's Flutter specialty filter) — real controller,
/// real MySQL, no auth.
/// </summary>
[Collection("Database")]
public class SpecialtyDiscoveryControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public SpecialtyDiscoveryControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
    {
        _dbFixture = dbFixture;
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsSeededSpecialty()
    {
        var marker = $"PublicSpecialtyTest{Guid.NewGuid():N}";

        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(_dbFixture.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        await using (var db = new AsnanDbContext(options))
        {
            db.Specialties.Add(new Specialty { Name = marker });
            await db.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().GetAsync("/api/v1/specialties");
        response.EnsureSuccessStatusCode();
        var specialties = await response.Content.ReadFromJsonAsync<List<SpecialtyDto>>();

        Assert.NotNull(specialties);
        Assert.Contains(specialties!, s => s.Name == marker);
    }
}
