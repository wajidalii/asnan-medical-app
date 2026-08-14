using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Asnan.Application.Appointments;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Application.Chat;
using Asnan.Application.Notifications;
using Asnan.Application.Payments;
using Asnan.Application.Reminders;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Chat;
using Asnan.Infrastructure.Payments;
using Asnan.Infrastructure.Persistence;
using Asnan.Infrastructure.Reminders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Asnan.Api.Tests;

/// <summary>
/// Trigger-wiring tests for issue #31 — each domain event (payment
/// succeeded/failed, cancellation, refund, reminder, doctor-availability
/// change, offline chat message) fires exactly one notification, using
/// CapturingNotificationSender per the issue's stated testing requirement.
///
/// Payment/refund events go through the real HTTP booking flow (the
/// pipeline is nontrivial enough that AppointmentsControllerTests/
/// PaymentsControllerTests already prove it out that way) with
/// INotificationSender swapped via WithWebHostBuilder. Reminder,
/// availability-change, and chat-offline events are simpler to exercise via
/// direct service construction (same style as ReminderSchedulingServiceTests).
/// </summary>
[Collection("Database")]
public class NotificationTriggerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _baseFactory;

    public NotificationTriggerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
    {
        _dbFixture = dbFixture;
        _baseFactory = factory;
    }

    private AsnanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AsnanDbContext>()
            .UseMySql(_dbFixture.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new AsnanDbContext(options);
    }

    // --- HTTP-flow helpers (payment/refund events) ---

    private (WebApplicationFactory<Program> Factory, CapturingNotificationSender Sender) CreateFactoryWithCapturingSender()
    {
        var sender = new CapturingNotificationSender();
        var factory = _baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INotificationSender>();
                services.AddSingleton<INotificationSender>(sender);
            }));
        return (factory, sender);
    }

    private async Task<(Guid DoctorId, Guid DoctorUserId)> SeedDoctorWithScheduleAsync(DateOnly date)
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(doctorUser);

        var doctor = new DoctorProfile
        {
            UserId = doctorUser.Id,
            FullName = "Dr. Notify Test",
            ConsultationFee = 100m,
            Currency = "USD",
            TimeZoneId = "UTC",
        };
        db.DoctorProfiles.Add(doctor);

        db.DoctorSchedules.Add(new DoctorSchedule
        {
            DoctorProfile = doctor,
            DayOfWeek = date.DayOfWeek,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            SlotDurationMinutes = 30,
            BufferMinutes = 0,
        });

        await db.SaveChangesAsync();
        return (doctor.Id, doctorUser.Id);
    }

    private async Task<(Guid UserId, string Token)> CreateUserTokenAsync(WebApplicationFactory<Program> factory)
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });
        await db.SaveChangesAsync();

        using var scope = factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { "Patient" });
        return (user.Id, token);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string bearerToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }

    private static Task RegisterDeviceAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/notifications/devices", new RegisterDeviceDto($"tok-{Guid.NewGuid()}", DevicePlatform.Android));

    private async Task<Guid> HoldAndCheckoutAsync(HttpClient patientClient, Guid doctorId, DateTime slotStartUtc, DateTime slotEndUtc)
    {
        var holdResponse = await patientClient.PostAsJsonAsync("/api/v1/appointments/holds", new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc));
        holdResponse.EnsureSuccessStatusCode();
        var hold = await holdResponse.Content.ReadFromJsonAsync<HoldDto>();

        var checkoutResponse = await patientClient.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(hold!.HoldToken));
        checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutDto>();
        return checkout!.AppointmentId;
    }

    private async Task ConfirmAndDeliverWebhookAsync(HttpClient patientClient, Guid appointmentId, bool succeeded)
    {
        await using var db = CreateDb();
        var transaction = await db.PaymentTransactions.FirstAsync(t => t.AppointmentId == appointmentId);

        var confirmResponse = await patientClient.PostAsJsonAsync(transaction.RedirectUrl, new { Succeeded = succeeded, FailureReason = succeeded ? null : "Card declined." });
        confirmResponse.EnsureSuccessStatusCode();
        var delivery = await confirmResponse.Content.ReadFromJsonAsync<MockWebhookDelivery>();

        var content = new StringContent(delivery!.RawBody, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook") { Content = content };
        request.Headers.Add("X-Mock-Signature", delivery.SignatureHeaderValue);
        var webhookResponse = await patientClient.SendAsync(request);
        webhookResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PaymentSucceeded_SendsExactlyOneAppointmentConfirmedNotification()
    {
        var (factory, sender) = CreateFactoryWithCapturingSender();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, token) = await CreateUserTokenAsync(factory);
        var client = CreateClient(factory, token);
        await RegisterDeviceAsync(client);

        var appointmentId = await HoldAndCheckoutAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));
        await ConfirmAndDeliverWebhookAsync(client, appointmentId, succeeded: true);

        Assert.Single(sender.Calls);
        var notification = sender.Calls[0].Notification;
        Assert.Equal($"asnan://appointments/{appointmentId}", notification.DeepLink);
        Assert.Contains("Dr. Notify Test", notification.Body);
    }

    [Fact]
    public async Task PaymentFailed_SendsExactlyOnePaymentFailedNotification()
    {
        var (factory, sender) = CreateFactoryWithCapturingSender();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(61));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, token) = await CreateUserTokenAsync(factory);
        var client = CreateClient(factory, token);
        await RegisterDeviceAsync(client);

        var appointmentId = await HoldAndCheckoutAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));
        await ConfirmAndDeliverWebhookAsync(client, appointmentId, succeeded: false);

        Assert.Single(sender.Calls);
        Assert.Equal($"asnan://appointments/{appointmentId}", sender.Calls[0].Notification.DeepLink);
        Assert.Equal("Payment failed", sender.Calls[0].Notification.Title);
    }

    [Fact]
    public async Task DuplicateWebhookDelivery_StillSendsExactlyOneNotification()
    {
        var (factory, sender) = CreateFactoryWithCapturingSender();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(62));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, token) = await CreateUserTokenAsync(factory);
        var client = CreateClient(factory, token);
        await RegisterDeviceAsync(client);

        var appointmentId = await HoldAndCheckoutAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));

        await using var db = CreateDb();
        var transaction = await db.PaymentTransactions.FirstAsync(t => t.AppointmentId == appointmentId);
        var confirmResponse = await client.PostAsJsonAsync(transaction.RedirectUrl, new { Succeeded = true, FailureReason = (string?)null });
        var delivery = await confirmResponse.Content.ReadFromJsonAsync<MockWebhookDelivery>();
        var content = new StringContent(delivery!.RawBody, Encoding.UTF8, "application/json");

        Func<Task> deliverOnce = async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook") { Content = new StringContent(delivery.RawBody, Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Mock-Signature", delivery.SignatureHeaderValue);
            (await client.SendAsync(request)).EnsureSuccessStatusCode();
        };

        await deliverOnce();
        await deliverOnce(); // same event id delivered twice — provider-side retry

        Assert.Single(sender.Calls);
    }

    [Fact]
    public async Task Cancel_WithSucceededPayment_SendsCancellationAndRefundNotifications()
    {
        var (factory, sender) = CreateFactoryWithCapturingSender();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(63));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, token) = await CreateUserTokenAsync(factory);
        var client = CreateClient(factory, token);
        await RegisterDeviceAsync(client);
        var appointmentId = await HoldAndCheckoutAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));
        await ConfirmAndDeliverWebhookAsync(client, appointmentId, succeeded: true);
        Assert.Single(sender.Calls); // the "confirmed" push from setup

        var cancelResponse = await client.PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new RequestCancelAppointmentDto("Change of plans"));
        cancelResponse.EnsureSuccessStatusCode();

        Assert.Equal(3, sender.Calls.Count); // confirmed + cancelled + refund completed
        var titles = sender.Calls.Select(c => c.Notification.Title).ToList();
        Assert.Contains("Appointment cancelled", titles);
        Assert.Contains("Refund completed", titles);
    }

    // --- Direct-service helpers (reminder / availability / chat events) ---

    private async Task<Appointment> SeedScheduledAppointmentAsync(AsnanDbContext db, DateTime slotStartUtc, string doctorTimeZoneId = "UTC")
    {
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.AddRange(doctorUser, patientUser);

        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Direct Test", ConsultationFee = 50m, Currency = "USD", TimeZoneId = doctorTimeZoneId };
        db.DoctorProfiles.Add(doctor);

        var appointment = new Appointment
        {
            DoctorProfileId = doctor.Id,
            PatientUserId = patientUser.Id,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            ConsultationFee = 50m,
            Currency = "USD",
            SourceHoldId = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);

        db.NotificationDevices.Add(new NotificationDevice { UserId = patientUser.Id, FcmToken = $"tok-{Guid.NewGuid()}", Platform = DevicePlatform.Android });

        await db.SaveChangesAsync();
        return appointment;
    }

    [Fact]
    public async Task ReminderDue_SendsExactlyOneNotificationPerAppointment()
    {
        await using var db = CreateDb();
        var appointment = await SeedScheduledAppointmentAsync(db, DateTime.UtcNow.AddMinutes(10));

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var reminderSender = new NotificationReminderSender(db, dispatch);
        var options = Microsoft.Extensions.Options.Options.Create(new ReminderOptions { OffsetsMinutes = [15] });
        var service = new ReminderSchedulingService(db, reminderSender, options);

        var sentCount = await service.ScanAndSendDueRemindersAsync(DateTime.UtcNow);

        Assert.Equal(1, sentCount);
        var call = Assert.Single(sender.Calls.Where(c => c.Notification.DeepLink == $"asnan://appointments/{appointment.Id}"));
        Assert.Equal("Appointment starting soon", call.Notification.Title);
    }

    [Fact]
    public async Task ReminderDue_RespectsOptOutOfTheRemindersCategory()
    {
        await using var db = CreateDb();
        var appointment = await SeedScheduledAppointmentAsync(db, DateTime.UtcNow.AddMinutes(10));
        db.NotificationPreferences.Add(new NotificationPreference { UserId = appointment.PatientUserId, Category = NotificationCategory.Reminders });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var reminderSender = new NotificationReminderSender(db, dispatch);
        var options = Microsoft.Extensions.Options.Options.Create(new ReminderOptions { OffsetsMinutes = [15] });
        var service = new ReminderSchedulingService(db, reminderSender, options);

        await service.ScanAndSendDueRemindersAsync(DateTime.UtcNow);

        Assert.DoesNotContain(sender.Calls, c => c.Notification.DeepLink == $"asnan://appointments/{appointment.Id}");
    }

    [Fact]
    public async Task LargerOffset_UsesReminderCopyNotStartingSoon()
    {
        await using var db = CreateDb();
        var appointment = await SeedScheduledAppointmentAsync(db, DateTime.UtcNow.AddMinutes(55));

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var reminderSender = new NotificationReminderSender(db, dispatch);
        var options = Microsoft.Extensions.Options.Options.Create(new ReminderOptions { OffsetsMinutes = [60] });
        var service = new ReminderSchedulingService(db, reminderSender, options);

        await service.ScanAndSendDueRemindersAsync(DateTime.UtcNow);

        var call = Assert.Single(sender.Calls.Where(c => c.Notification.DeepLink == $"asnan://appointments/{appointment.Id}"));
        Assert.Equal("Appointment reminder", call.Notification.Title);
    }

    [Fact]
    public async Task AvailabilityException_UnavailableOverlappingAppointment_NotifiesThePatientExactlyOnce()
    {
        await using var db = CreateDb();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(70));
        var appointment = await SeedScheduledAppointmentAsync(db, new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc));
        var doctor = await db.DoctorProfiles.FirstAsync(d => d.Id == appointment.DoctorProfileId);

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var service = new AvailabilityExceptionService(db, dispatch);
        var caller = new Asnan.Application.Common.CallerContext(doctor.UserId, IsAdmin: false);

        var result = await service.CreateAsync(
            doctor.Id,
            new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.Unavailable, null, null, "Sick day"),
            caller);

        Assert.Equal(AvailabilityExceptionMutationStatus.Success, result.Status);
        var call = Assert.Single(sender.Calls);
        Assert.Equal($"asnan://appointments/{appointment.Id}", call.Notification.DeepLink);
    }

    [Fact]
    public async Task AvailabilityException_ExtraAvailability_DoesNotNotifyAnyone()
    {
        await using var db = CreateDb();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(71));
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(doctorUser);
        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Extra Hours", ConsultationFee = 50m, Currency = "USD", TimeZoneId = "UTC" };
        db.DoctorProfiles.Add(doctor);
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var service = new AvailabilityExceptionService(db, dispatch);
        var caller = new Asnan.Application.Common.CallerContext(doctor.UserId, IsAdmin: false);

        var result = await service.CreateAsync(
            doctor.Id,
            new CreateAvailabilityExceptionDto(date, AvailabilityExceptionType.ExtraAvailability, new TimeOnly(18, 0), new TimeOnly(20, 0), "Extra evening slots"),
            caller);

        Assert.Equal(AvailabilityExceptionMutationStatus.Success, result.Status);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task OfflineChatMessage_NotifiesTheOfflineRecipientExactlyOnce()
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.AddRange(doctorUser, patientUser);
        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Chat Test", ConsultationFee = 50m, Currency = "USD", TimeZoneId = "UTC" };
        db.DoctorProfiles.Add(doctor);
        var appointment = new Appointment
        {
            DoctorProfileId = doctor.Id,
            PatientUserId = patientUser.Id,
            SlotStartUtc = DateTime.UtcNow.AddDays(1),
            SlotEndUtc = DateTime.UtcNow.AddDays(1).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            ConsultationFee = 50m,
            Currency = "USD",
            SourceHoldId = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);
        var conversation = new ChatConversation { Appointment = appointment };
        db.ChatConversations.Add(conversation);
        db.NotificationDevices.Add(new NotificationDevice { UserId = patientUser.Id, FcmToken = $"tok-{Guid.NewGuid()}", Platform = DevicePlatform.Android });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var notifier = new NotificationOfflineMessageNotifier(db, dispatch);

        await notifier.NotifyAsync(patientUser.Id, conversation.Id, Guid.NewGuid());

        var call = Assert.Single(sender.Calls);
        Assert.Equal($"asnan://chat/{conversation.Id}", call.Notification.DeepLink);
        Assert.Contains("Dr. Chat Test", call.Notification.Body);
    }

    [Fact]
    public async Task OfflineChatMessage_DoctorRecipient_UsesGenericPatientLabel()
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.AddRange(doctorUser, patientUser);
        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Recipient Test", ConsultationFee = 50m, Currency = "USD", TimeZoneId = "UTC" };
        db.DoctorProfiles.Add(doctor);
        var appointment = new Appointment
        {
            DoctorProfileId = doctor.Id,
            PatientUserId = patientUser.Id,
            SlotStartUtc = DateTime.UtcNow.AddDays(1),
            SlotEndUtc = DateTime.UtcNow.AddDays(1).AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            ConsultationFee = 50m,
            Currency = "USD",
            SourceHoldId = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);
        var conversation = new ChatConversation { Appointment = appointment };
        db.ChatConversations.Add(conversation);
        db.NotificationDevices.Add(new NotificationDevice { UserId = doctorUser.Id, FcmToken = $"tok-{Guid.NewGuid()}", Platform = DevicePlatform.Android });
        await db.SaveChangesAsync();

        var sender = new CapturingNotificationSender();
        var dispatch = new NotificationDispatchService(db, sender);
        var notifier = new NotificationOfflineMessageNotifier(db, dispatch);

        await notifier.NotifyAsync(doctorUser.Id, conversation.Id, Guid.NewGuid());

        var call = Assert.Single(sender.Calls);
        Assert.Contains("a patient", call.Notification.Body);
    }
}
