using Asnan.Application.Notifications;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Api.Tests;

/// <summary>
/// Preference-filtering logic for NotificationDispatchService (issue #31's
/// explicit testing requirement) — real MySQL, a CapturingNotificationSender
/// standing in for the real push provider.
/// </summary>
[Collection("Database")]
public class NotificationDispatchServiceTests
{
    private readonly DatabaseFixture _dbFixture;

    public NotificationDispatchServiceTests(DatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    private AsnanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(_dbFixture.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new AsnanDbContext(options);
    }

    private static PushNotification SampleNotification() => new("Title", "Body", "asnan://appointments/x");

    [Fact]
    public async Task DispatchAsync_UserWithNoDevices_DoesNotSendAndLeavesNoHistory()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var service = new NotificationDispatchService(db, sender);

        await service.DispatchAsync(user.Id, NotificationCategory.AppointmentUpdates, SampleNotification());

        Assert.Empty(sender.Calls);
        Assert.False(await db.Notifications.AnyAsync(n => n.UserId == user.Id));
    }

    [Fact]
    public async Task DispatchAsync_UserWithADevice_SendsExactlyOnceAndRecordsHistory()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.NotificationDevices.Add(new NotificationDevice { UserId = user.Id, FcmToken = $"tok-{Guid.NewGuid()}", Platform = DevicePlatform.Android });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var service = new NotificationDispatchService(db, sender);
        var notification = SampleNotification();

        await service.DispatchAsync(user.Id, NotificationCategory.AppointmentUpdates, notification);

        Assert.Single(sender.Calls);
        Assert.Equal(notification.Title, sender.Calls[0].Notification.Title);

        var history = await db.Notifications.SingleAsync(n => n.UserId == user.Id);
        Assert.Equal(NotificationCategory.AppointmentUpdates, history.Category);
        Assert.Equal(notification.DeepLink, history.DeepLink);
    }

    [Fact]
    public async Task DispatchAsync_DisableableCategoryOptedOut_DoesNotSend()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.NotificationDevices.Add(new NotificationDevice { UserId = user.Id, FcmToken = $"tok-{Guid.NewGuid()}", Platform = DevicePlatform.Android });
        db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Category = NotificationCategory.Reminders });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var service = new NotificationDispatchService(db, sender);

        await service.DispatchAsync(user.Id, NotificationCategory.Reminders, SampleNotification());

        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task DispatchAsync_NonDisableableCategory_IgnoresAnyOptOutRowAndStillSends()
    {
        // A row for a non-disableable category should never exist via the
        // real API (NotificationPreferenceService rejects it), but the
        // dispatcher itself must not honor one even if it somehow did —
        // ARCHITECTURE.md §10's "not user-disable-able" guarantee lives here,
        // not just at the preferences-API layer.
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.NotificationDevices.Add(new NotificationDevice { UserId = user.Id, FcmToken = $"tok-{Guid.NewGuid()}", Platform = DevicePlatform.Android });
        db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Category = NotificationCategory.AppointmentUpdates });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var service = new NotificationDispatchService(db, sender);

        await service.DispatchAsync(user.Id, NotificationCategory.AppointmentUpdates, SampleNotification());

        Assert.Single(sender.Calls);
    }

    [Fact]
    public async Task DispatchAsync_SendsToEveryRegisteredDeviceToken()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.NotificationDevices.Add(new NotificationDevice { UserId = user.Id, FcmToken = $"tok-a-{Guid.NewGuid()}", Platform = DevicePlatform.Android });
        db.NotificationDevices.Add(new NotificationDevice { UserId = user.Id, FcmToken = $"tok-b-{Guid.NewGuid()}", Platform = DevicePlatform.Ios });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var service = new NotificationDispatchService(db, sender);

        await service.DispatchAsync(user.Id, NotificationCategory.ChatMessages, SampleNotification());

        Assert.Single(sender.Calls); // one SendAsync call, batched across both tokens
        Assert.Equal(2, sender.Calls[0].Tokens.Count);
    }
}
