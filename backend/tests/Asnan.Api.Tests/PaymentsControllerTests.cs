using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Application.Payments;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Payments;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for the checkout + webhook flow (issue
/// #20) — real controller, real MySQL, real mock provider, exercising the
/// four scenarios the issue's testing requirement calls out: happy path,
/// duplicate webhook delivery, payment failure, and an expired hold at
/// webhook time.
/// </summary>
[Collection("Database")]
public class PaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid DoctorId, Guid DoctorUserId, DateTime SlotStartUtc, DateTime SlotEndUtc)> SeedDoctorWithScheduleAsync()
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(doctorUser);

        var doctor = new DoctorProfile
        {
            UserId = doctorUser.Id,
            FullName = "Dr. Payment Test",
            ConsultationFee = 120m,
            Currency = "USD",
            TimeZoneId = "UTC",
        };
        db.DoctorProfiles.Add(doctor);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
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

        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        return (doctor.Id, doctorUser.Id, slotStartUtc, slotStartUtc.AddMinutes(30));
    }

    private async Task<(Guid UserId, string Token)> CreatePatientTokenAsync()
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });
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

    private static async Task<string> CreateHoldAsync(HttpClient client, Guid doctorId, DateTime slotStartUtc, DateTime slotEndUtc)
    {
        var response = await client.PostAsJsonAsync("/api/v1/appointments/holds", new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc));
        response.EnsureSuccessStatusCode();
        var hold = await response.Content.ReadFromJsonAsync<HoldDto>();
        return hold!.HoldToken;
    }

    private static async Task<HttpResponseMessage> DeliverWebhookAsync(HttpClient client, MockWebhookDelivery delivery)
    {
        var content = new StringContent(delivery.RawBody, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook") { Content = content };
        request.Headers.Add("X-Mock-Signature", delivery.SignatureHeaderValue);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Checkout_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto("irrelevant-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_ForUnknownHoldToken_ReturnsNotFound()
    {
        var (_, token) = await CreatePatientTokenAsync();

        var response = await CreateClient(token).PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto("not-a-real-token"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_CalledTwiceForTheSameHold_ReturnsTheSameSession()
    {
        var (doctorId, _, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, token) = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var holdToken = await CreateHoldAsync(client, doctorId, slotStartUtc, slotEndUtc);

        var first = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();
        var second = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();

        Assert.Equal(first!.AppointmentId, second!.AppointmentId);
        Assert.Equal(first.ProviderSessionId, second.ProviderSessionId);
    }

    [Fact]
    public async Task HappyPath_CheckoutConfirmWebhook_SchedulesAppointmentAndCreatesChatConversation()
    {
        var (doctorId, doctorUserId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (patientId, token) = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var holdToken = await CreateHoldAsync(client, doctorId, slotStartUtc, slotEndUtc);

        var checkout = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();
        Assert.NotNull(checkout);
        Assert.Equal(AppointmentStatus.PaymentPending, checkout!.Status);

        var confirmResponse = await client.PostAsJsonAsync(checkout.RedirectUrl, new { Succeeded = true, FailureReason = (string?)null });
        confirmResponse.EnsureSuccessStatusCode();
        var delivery = await confirmResponse.Content.ReadFromJsonAsync<MockWebhookDelivery>();

        var webhookResponse = await DeliverWebhookAsync(client, delivery!);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        await using var db = CreateDb();
        var appointment = await db.Appointments.FirstAsync(a => a.Id == checkout.AppointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);

        var conversation = await db.ChatConversations.Include(c => c.Participants).FirstOrDefaultAsync(c => c.AppointmentId == appointment.Id);
        Assert.NotNull(conversation);
        Assert.Equal(2, conversation!.Participants.Count);
        Assert.Contains(conversation.Participants, p => p.UserId == patientId);
        Assert.Contains(conversation.Participants, p => p.UserId == doctorUserId);

        var transaction = await db.PaymentTransactions.FirstAsync(t => t.AppointmentId == appointment.Id);
        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);

        // The mobile confirmation screen (#22) polls this same idempotent endpoint to observe Scheduled.
        var polled = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();
        Assert.Equal(AppointmentStatus.Scheduled, polled!.Status);

        var hold = await db.AppointmentHolds.FirstAsync(h => h.PatientUserId == patientId);
        Assert.Equal(HoldStatus.Consumed, hold.Status);
    }

    [Fact]
    public async Task DuplicateWebhookDelivery_IsANoOp()
    {
        var (doctorId, _, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, token) = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var holdToken = await CreateHoldAsync(client, doctorId, slotStartUtc, slotEndUtc);
        var checkout = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();
        var delivery = await (await client.PostAsJsonAsync(checkout!.RedirectUrl, new { Succeeded = true, FailureReason = (string?)null })).Content.ReadFromJsonAsync<MockWebhookDelivery>();

        var first = await DeliverWebhookAsync(client, delivery!);
        var second = await DeliverWebhookAsync(client, delivery!);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await using var db = CreateDb();
        var conversationCount = await db.ChatConversations.CountAsync(c => c.AppointmentId == checkout.AppointmentId);
        Assert.Equal(1, conversationCount);

        var historyCount = await db.AppointmentStatusHistories.CountAsync(h => h.AppointmentId == checkout.AppointmentId && h.ToStatus == AppointmentStatus.Scheduled);
        Assert.Equal(1, historyCount);
    }

    [Fact]
    public async Task DuplicateWebhookDelivery_UnderConcurrentDelivery_IsStillANoOp()
    {
        // Same scenario as DuplicateWebhookDelivery_IsANoOp, but the duplicate
        // arrives concurrently rather than sequentially — proves the
        // ProcessedWebhookEvents unique-index race handling (PaymentService's
        // catch (DbUpdateException) branch) actually holds under real
        // simultaneous delivery, not just back-to-back awaited calls.
        var (doctorId, _, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, token) = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var holdToken = await CreateHoldAsync(client, doctorId, slotStartUtc, slotEndUtc);
        var checkout = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();
        var delivery = await (await client.PostAsJsonAsync(checkout!.RedirectUrl, new { Succeeded = true, FailureReason = (string?)null })).Content.ReadFromJsonAsync<MockWebhookDelivery>();

        const int concurrency = 8;
        var responses = await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => DeliverWebhookAsync(client, delivery!)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        await using var db = CreateDb();
        var conversationCount = await db.ChatConversations.CountAsync(c => c.AppointmentId == checkout.AppointmentId);
        Assert.Equal(1, conversationCount);

        var historyCount = await db.AppointmentStatusHistories.CountAsync(h => h.AppointmentId == checkout.AppointmentId && h.ToStatus == AppointmentStatus.Scheduled);
        Assert.Equal(1, historyCount);

        var processedEventCount = await db.ProcessedWebhookEvents.CountAsync(e => e.ProviderEventId == delivery!.ProviderEventId);
        Assert.Equal(1, processedEventCount);
    }

    [Fact]
    public async Task FailedPayment_LeavesAppointmentUnscheduled_AndReleasesTheHold()
    {
        var (doctorId, _, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (patientId, token) = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var holdToken = await CreateHoldAsync(client, doctorId, slotStartUtc, slotEndUtc);
        var checkout = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();

        var delivery = await (await client.PostAsJsonAsync(checkout!.RedirectUrl, new { Succeeded = false, FailureReason = "Card declined" })).Content.ReadFromJsonAsync<MockWebhookDelivery>();
        var webhookResponse = await DeliverWebhookAsync(client, delivery!);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        await using var db = CreateDb();
        var appointment = await db.Appointments.FirstAsync(a => a.Id == checkout.AppointmentId);
        Assert.Equal(AppointmentStatus.PaymentFailed, appointment.Status);

        var hold = await db.AppointmentHolds.FirstAsync(h => h.PatientUserId == patientId);
        Assert.Equal(HoldStatus.Released, hold.Status);
        Assert.Null(hold.ActiveSlotKey);
    }

    [Fact]
    public async Task SucceededPayment_ForAHoldThatExpiredBeforeTheWebhookArrived_DoesNotScheduleOrOrphanTheAppointment()
    {
        var (doctorId, _, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, token) = await CreatePatientTokenAsync();
        var client = CreateClient(token);
        var holdToken = await CreateHoldAsync(client, doctorId, slotStartUtc, slotEndUtc);
        var checkout = await (await client.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(holdToken))).Content.ReadFromJsonAsync<CheckoutDto>();

        await using (var db = CreateDb())
        {
            var appointment = await db.Appointments.FirstAsync(a => a.Id == checkout!.AppointmentId);
            var hold = await db.AppointmentHolds.FirstAsync(h => h.Id == appointment.SourceHoldId);
            hold.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var delivery = await (await client.PostAsJsonAsync(checkout!.RedirectUrl, new { Succeeded = true, FailureReason = (string?)null })).Content.ReadFromJsonAsync<MockWebhookDelivery>();
        var webhookResponse = await DeliverWebhookAsync(client, delivery!);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        await using var verifyDb = CreateDb();
        var updatedAppointment = await verifyDb.Appointments.FirstAsync(a => a.Id == checkout.AppointmentId);
        Assert.Equal(AppointmentStatus.PaymentFailed, updatedAppointment.Status);

        var conversationExists = await verifyDb.ChatConversations.AnyAsync(c => c.AppointmentId == checkout.AppointmentId);
        Assert.False(conversationExists);

        var transaction = await verifyDb.PaymentTransactions.FirstAsync(t => t.AppointmentId == checkout.AppointmentId);
        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Mock-Signature", "not-a-real-signature");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
