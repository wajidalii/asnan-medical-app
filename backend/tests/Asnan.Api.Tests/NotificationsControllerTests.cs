using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asnan.Application.Auth;
using Asnan.Application.Notifications;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for device registration + preference
/// filtering (issue #30) — real controller, real MySQL. Covers the issue's
/// explicit testing requirement ("preference filtering logic") from the
/// REST surface, complementing NotificationSenderSelectionTests' unit
/// coverage of sender selection.
/// </summary>
[Collection("Database")]
public class NotificationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public NotificationsControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid UserId, string Token)> CreateUserTokenAsync()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { "Patient" });
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
    public async Task RegisterDevice_PersistsANewDeviceForTheCaller()
    {
        var (userId, token) = await CreateUserTokenAsync();
        var fcmToken = $"token-{Guid.NewGuid()}";

        var response = await CreateClient(token).PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto(fcmToken, DevicePlatform.Android));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = CreateDb();
        var device = await db.NotificationDevices.SingleAsync(d => d.FcmToken == fcmToken);
        Assert.Equal(userId, device.UserId);
        Assert.Equal(DevicePlatform.Android, device.Platform);
    }

    [Fact]
    public async Task RegisterDevice_SameTokenDifferentUser_ReassignsOwnership()
    {
        var (_, firstToken) = await CreateUserTokenAsync();
        var (secondUserId, secondToken) = await CreateUserTokenAsync();
        var fcmToken = $"token-{Guid.NewGuid()}";

        await CreateClient(firstToken).PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto(fcmToken, DevicePlatform.Android));
        var response = await CreateClient(secondToken).PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto(fcmToken, DevicePlatform.Ios));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = CreateDb();
        var device = await db.NotificationDevices.SingleAsync(d => d.FcmToken == fcmToken);
        Assert.Equal(secondUserId, device.UserId);
        Assert.Equal(DevicePlatform.Ios, device.Platform);
    }

    [Fact]
    public async Task RegisterDevice_MissingToken_ReturnsBadRequest()
    {
        var (_, token) = await CreateUserTokenAsync();

        var response = await CreateClient(token).PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto("", DevicePlatform.Android));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveDevice_DeletesTheCallersOwnDevice()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);
        var fcmToken = $"token-{Guid.NewGuid()}";
        await client.PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto(fcmToken, DevicePlatform.Android));

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/notifications/devices")
        {
            Content = JsonContent.Create(new RemoveDeviceDto(fcmToken)),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = CreateDb();
        Assert.False(await db.NotificationDevices.AnyAsync(d => d.FcmToken == fcmToken));
    }

    [Fact]
    public async Task RemoveDevice_OwnedBySomeoneElse_LeavesItUntouched()
    {
        var (_, ownerToken) = await CreateUserTokenAsync();
        var (_, strangerToken) = await CreateUserTokenAsync();
        var fcmToken = $"token-{Guid.NewGuid()}";
        await CreateClient(ownerToken).PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto(fcmToken, DevicePlatform.Android));

        var response = await CreateClient(strangerToken).SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/notifications/devices")
        {
            Content = JsonContent.Create(new RemoveDeviceDto(fcmToken)),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); // intentionally idempotent, see INotificationDeviceService.RemoveAsync

        await using var db = CreateDb();
        Assert.True(await db.NotificationDevices.AnyAsync(d => d.FcmToken == fcmToken));
    }

    [Fact]
    public async Task GetPreferences_DefaultsEveryCategoryToEnabled_WithCorrectDisableableFlags()
    {
        var (_, token) = await CreateUserTokenAsync();

        var response = await CreateClient(token).GetAsync("/api/v1/notifications/preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preferences = await response.Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>();
        Assert.Equal(Enum.GetValues<NotificationCategory>().Length, preferences!.Count);
        Assert.All(preferences, p => Assert.True(p.IsEnabled));

        var appointmentUpdates = preferences.Single(p => p.Category == NotificationCategory.AppointmentUpdates);
        Assert.False(appointmentUpdates.IsDisableable);
        var reminders = preferences.Single(p => p.Category == NotificationCategory.Reminders);
        Assert.True(reminders.IsDisableable);
    }

    [Fact]
    public async Task SetPreference_DisablingADisableableCategory_PersistsAndReflectsOnNextGet()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);

        var setResponse = await client.PutAsJsonAsync("/api/v1/notifications/preferences/Reminders", new SetNotificationPreferenceDto(false));
        Assert.Equal(HttpStatusCode.NoContent, setResponse.StatusCode);

        var preferences = await (await client.GetAsync("/api/v1/notifications/preferences")).Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>();
        Assert.False(preferences!.Single(p => p.Category == NotificationCategory.Reminders).IsEnabled);
    }

    [Fact]
    public async Task SetPreference_ReEnablingAfterDisabling_RestoresTheDefault()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);
        await client.PutAsJsonAsync("/api/v1/notifications/preferences/ChatMessages", new SetNotificationPreferenceDto(false));

        var setResponse = await client.PutAsJsonAsync("/api/v1/notifications/preferences/ChatMessages", new SetNotificationPreferenceDto(true));

        Assert.Equal(HttpStatusCode.NoContent, setResponse.StatusCode);
        var preferences = await (await client.GetAsync("/api/v1/notifications/preferences")).Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>();
        Assert.True(preferences!.Single(p => p.Category == NotificationCategory.ChatMessages).IsEnabled);
    }

    [Fact]
    public async Task SetPreference_DisablingANonDisableableCategory_ReturnsBadRequestAndDoesNotPersist()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);

        var response = await client.PutAsJsonAsync("/api/v1/notifications/preferences/PaymentUpdates", new SetNotificationPreferenceDto(false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var preferences = await (await client.GetAsync("/api/v1/notifications/preferences")).Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>();
        Assert.True(preferences!.Single(p => p.Category == NotificationCategory.PaymentUpdates).IsEnabled);
    }

    [Fact]
    public async Task Preferences_AreIsolatedPerUser()
    {
        var (_, ownerToken) = await CreateUserTokenAsync();
        var (_, strangerToken) = await CreateUserTokenAsync();
        await CreateClient(ownerToken).PutAsJsonAsync("/api/v1/notifications/preferences/Reminders", new SetNotificationPreferenceDto(false));

        var strangerPreferences = await (await CreateClient(strangerToken).GetAsync("/api/v1/notifications/preferences"))
            .Content.ReadFromJsonAsync<List<NotificationPreferenceDto>>();

        Assert.True(strangerPreferences!.Single(p => p.Category == NotificationCategory.Reminders).IsEnabled);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/notifications/preferences")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto("t", DevicePlatform.Android))).StatusCode);
    }
}
