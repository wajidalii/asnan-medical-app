using Asnan.Application.Auth;
using Asnan.Application.Common;
using Asnan.Application.Otps;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Otps;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // Fixed server version (rather than ServerVersion.AutoDetect) so that registering
        // services never requires a live database connection at startup.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<AsnanDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AsnanDbContext>());

        AddOtpProviders(services, configuration, isDevelopment);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    private static void AddOtpProviders(IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var emailProvider = configuration["OtpProvider:Email"] ?? "Mock";
        var smsProvider = configuration["OtpProvider:Sms"] ?? "Mock";

        RequireDevelopmentIfMock(emailProvider, "OtpProvider:Email", isDevelopment);
        RequireDevelopmentIfMock(smsProvider, "OtpProvider:Sms", isDevelopment);

        // Only "Mock" exists today; real providers register here behind the
        // same interfaces once credentials exist (see issue #39).
        services.AddScoped<IEmailOtpSender, MockEmailOtpSender>();
        services.AddScoped<ISmsOtpSender, MockSmsOtpSender>();

        services.AddScoped<IOtpSender, CompositeOtpSender>();
    }

    private static void RequireDevelopmentIfMock(string provider, string configKey, bool isDevelopment)
    {
        // Only a mock implementation exists today (see issue #39 for real-provider
        // tracking), so anything other than an explicit non-mock provider name
        // resolves to the mock — and the mock is refused outside Development.
        var isMock = string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase);

        if (isMock && !isDevelopment)
        {
            throw new InvalidOperationException(
                $"{configKey} has no real provider configured, and the mock provider is not permitted outside " +
                "the Development environment. Configure a real provider before deploying.");
        }
    }
}
