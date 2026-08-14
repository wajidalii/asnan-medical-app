using System.Security.Cryptography;
using System.Text.Json;
using Asnan.Application.Notifications;
using Asnan.Infrastructure;
using Asnan.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// Unit tests for INotificationSender selection (issue #30's explicit
/// "unit tests for the sender abstraction selection" requirement) — exercises
/// AddInfrastructure directly against an in-memory configuration, no
/// database/HTTP host needed (registering services never opens a real
/// connection, see DependencyInjection's comment).
/// </summary>
public class NotificationSenderSelectionTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? extraConfig = null)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=127.0.0.1;Port=3306;Database=test;User=test;Password=test;",
        };
        if (extraConfig is not null)
        {
            foreach (var (key, value) in extraConfig)
            {
                configValues[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, isDevelopment: true);
        return services.BuildServiceProvider();
    }

    /// <summary>A syntactically valid (but throwaway) service-account JSON — GoogleCredential construction is purely local/offline, no network call happens until a token is actually requested.</summary>
    private static string FakeFcmCredentialsJson()
    {
        using var rsa = RSA.Create(2048);
        var serviceAccount = new
        {
            type = "service_account",
            project_id = "test-project",
            private_key_id = "test-key-id",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            client_email = "test@test-project.iam.gserviceaccount.com",
            client_id = "123456789",
            auth_uri = "https://accounts.google.com/o/oauth2/auth",
            token_uri = "https://oauth2.googleapis.com/token",
            auth_provider_x509_cert_url = "https://www.googleapis.com/oauth2/v1/certs",
            client_x509_cert_url = "https://www.googleapis.com/robot/v1/metadata/x509/test%40test-project.iam.gserviceaccount.com",
        };
        return JsonSerializer.Serialize(serviceAccount);
    }

    [Fact]
    public void NoProviderConfigured_ResolvesToNoOpSender()
    {
        using var provider = BuildProvider();

        var sender = provider.GetRequiredService<INotificationSender>();

        Assert.IsType<NoOpNotificationSender>(sender);
    }

    [Fact]
    public void UnrecognizedProvider_ResolvesToNoOpSender()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Notification:Provider"] = "SomethingElse" });

        var sender = provider.GetRequiredService<INotificationSender>();

        Assert.IsType<NoOpNotificationSender>(sender);
    }

    [Fact]
    public void FcmProviderWithCredentials_ResolvesToFcmSender()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Notification:Provider"] = "Fcm",
            ["Notification:Fcm:CredentialsJson"] = FakeFcmCredentialsJson(),
        });

        var sender = provider.GetRequiredService<INotificationSender>();

        Assert.IsType<FcmNotificationSender>(sender);
    }

    [Fact]
    public void FcmProviderIsCaseInsensitive()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Notification:Provider"] = "fcm",
            ["Notification:Fcm:CredentialsJson"] = FakeFcmCredentialsJson(),
        });

        var sender = provider.GetRequiredService<INotificationSender>();

        Assert.IsType<FcmNotificationSender>(sender);
    }

    [Fact]
    public void FcmProviderWithoutCredentials_ThrowsAtRegistrationTime()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=127.0.0.1;Port=3306;Database=test;User=test;Password=test;",
            ["Notification:Provider"] = "Fcm",
        }).Build();
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration, isDevelopment: true));
    }

    [Fact]
    public void FcmProviderConfiguredEvenOutsideDevelopment_DoesNotThrow()
    {
        // Unlike RequireDevelopmentIfMock (Otp/Payment), NoOp is never
        // refused in a non-Development environment — see
        // NoOpNotificationSender's doc comment. OtpProvider/Payment:Provider
        // are set to a non-"Mock" placeholder purely so their own dev-gate
        // doesn't throw first and mask the assertion this test cares about.
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=127.0.0.1;Port=3306;Database=test;User=test;Password=test;",
            ["OtpProvider:Email"] = "Real",
            ["OtpProvider:Sms"] = "Real",
            ["Payment:Provider"] = "Real",
        }).Build();

        services.AddInfrastructure(configuration, isDevelopment: false);
        using var nonDevProvider = services.BuildServiceProvider();

        Assert.IsType<NoOpNotificationSender>(nonDevProvider.GetRequiredService<INotificationSender>());
    }
}
