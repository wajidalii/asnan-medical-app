using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // Fixed server version (rather than ServerVersion.AutoDetect) so that registering
        // services never requires a live database connection at startup.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<AsnanDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        return services;
    }
}
