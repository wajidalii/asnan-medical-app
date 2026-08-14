using Asnan.Application.Auth;
using Asnan.Application.Chat;
using Asnan.Application.Common;
using Asnan.Application.Notifications;
using Asnan.Application.Otps;
using Asnan.Application.Payments;
using Asnan.Application.Reminders;
using Asnan.Infrastructure.Auth;
using Asnan.Infrastructure.Chat;
using Asnan.Infrastructure.Notifications;
using Asnan.Infrastructure.Otps;
using Asnan.Infrastructure.Payments;
using Asnan.Infrastructure.Persistence;
using Asnan.Infrastructure.Reminders;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
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

        // Real push delivery (issue #31) — both go through
        // INotificationDispatchService, so they inherit its NoOp/Fcm
        // selection and NotificationPreferences filtering automatically.
        services.AddScoped<IReminderSender, NotificationReminderSender>();
        services.AddScoped<IOfflineMessageNotifier, NotificationOfflineMessageNotifier>();

        AddNotificationSender(services, configuration);

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

    /// <summary>
    /// Unlike AddOtpProviders/AddPaymentProvider, this is deliberately NOT
    /// gated to Development — see NoOpNotificationSender's doc comment for
    /// why logging instead of pushing carries no safety implication. Only
    /// explicitly selecting "Fcm" without configured credentials is treated
    /// as a hard misconfiguration (fail fast rather than silently drop pushes).
    /// </summary>
    private static void AddNotificationSender(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Notification:Provider"] ?? string.Empty;

        if (!string.Equals(provider, "Fcm", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<INotificationSender, NoOpNotificationSender>();
            return;
        }

        var credentialsJson = configuration["Notification:Fcm:CredentialsJson"];
        if (string.IsNullOrWhiteSpace(credentialsJson))
        {
            throw new InvalidOperationException(
                "Notification:Provider is \"Fcm\" but Notification:Fcm:CredentialsJson is not configured.");
        }

        var app = GetOrCreateFirebaseApp(credentialsJson);
        services.AddSingleton(FirebaseMessaging.GetMessaging(app));
        services.AddScoped<INotificationSender, FcmNotificationSender>();
    }

    /// <summary>FirebaseApp.Create throws if an app with the given name already exists in this process — guard for host restarts within the same process (e.g. test hosts). GetInstance returns null (not an exception) when no such app exists yet.</summary>
    private static FirebaseApp GetOrCreateFirebaseApp(string credentialsJson)
    {
        const string appName = "asnan-fcm";

        var existing = FirebaseApp.GetInstance(appName);
        if (existing is not null)
        {
            return existing;
        }

        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(credentialsJson).ToGoogleCredential();
        return FirebaseApp.Create(new AppOptions { Credential = credential }, appName);
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
