using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Asnan.Api.Tests;

/// <summary>
/// Applies migrations exactly once for every test class that touches a real
/// database. Without this, two test classes each calling MigrateAsync()
/// independently race on CREATE TABLE against a fresh database (this is
/// exactly what happened in CI — passed locally only because the dev DB was
/// already migrated ahead of time, masking the race).
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        ConnectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Port=3307;Database=asnan_dev;User=asnan;Password=asnan_dev_only_password;";

        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        await using var db = new AsnanDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
