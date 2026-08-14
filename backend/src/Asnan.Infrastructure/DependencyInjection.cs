using Asnan.Application.Auth;
using Asnan.Application.Chat;
using Asnan.Application.Common;
using Asnan.Application.Otps;
using Asnan.Application.Payments;
using Asnan.Application.Reminders;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Chat;
using Asnan.Infrastructure.Otps;
using Asnan.Infrastructure.Payments;
using Asnan.Infrastructure.Persistence;
using Asnan.Infrastructure.Reminders;
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
        AddPaymentProvider(services, configuration, isDevelopment);

        // Unlike the OTP/payment mocks, this stub isn't dev-gated: logging
        // instead of pushing has no safety implications, it just means
        // reminders aren't actually delivered until Milestone 8 replaces it.
        services.AddScoped<IReminderSender, LoggingReminderSender>();

        // Same rationale as IReminderSender — not dev-gated.
        services.AddScoped<IOfflineMessageNotifier, LoggingOfflineMessageNotifier>();

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

    private static void AddPaymentProvider(IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var provider = configuration["Payment:Provider"] ?? "Mock";

        RequireDevelopmentIfMock(provider, "Payment:Provider", isDevelopment);

        // Only "Mock" exists today; a real provider (Stripe is the leading
        // candidate) registers here behind the same IPaymentProvider once
        // credentials exist — see issue #60.
        services.AddSingleton<MockPaymentProviderStore>();
        services.AddScoped<MockPaymentProvider>();
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<MockPaymentProvider>());
        services.AddScoped<IMockPaymentProviderConfirmation>(sp => sp.GetRequiredService<MockPaymentProvider>());
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
