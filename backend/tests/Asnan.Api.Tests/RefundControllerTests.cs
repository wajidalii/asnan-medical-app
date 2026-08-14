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
/// HTTP-level integration test for the full cancel→refund flow (issue #21)
/// against the mock provider, per the issue's testing requirement.
/// </summary>
[Collection("Database")]
public class RefundControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public RefundControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid DoctorId, DateTime SlotStartUtc, DateTime SlotEndUtc)> SeedDoctorWithScheduleAsync()
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(doctorUser);

        var doctor = new DoctorProfile
        {
            UserId = doctorUser.Id,
            FullName = "Dr. Refund Test",
            ConsultationFee = 200m,
            Currency = "USD",
            TimeZoneId = "UTC",
        };
        db.DoctorProfiles.Add(doctor);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35));
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
        return (doctor.Id, slotStartUtc, slotStartUtc.AddMinutes(30));
    }

    private async Task<(Guid UserId, string Token)> CreateUserTokenAsync(string role)
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        var roleId = role == "Admin" ? RoleIds.Admin : RoleIds.Patient;
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { role });
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

    private async Task<Guid> BookAndScheduleAppointmentAsync(HttpClient patientClient, Guid doctorId, DateTime slotStartUtc, DateTime slotEndUtc)
    {
        var holdResponse = await patientClient.PostAsJsonAsync("/api/v1/appointments/holds", new CreateHoldDto(doctorId, slotStartUtc, slotEndUtc));
        holdResponse.EnsureSuccessStatusCode();
        var hold = await holdResponse.Content.ReadFromJsonAsync<HoldDto>();

        var checkoutResponse = await patientClient.PostAsJsonAsync("/api/v1/payments/checkout", new CreateCheckoutDto(hold!.HoldToken));
        checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutDto>();

        var confirmResponse = await patientClient.PostAsJsonAsync(checkout!.RedirectUrl, new { Succeeded = true, FailureReason = (string?)null });
        confirmResponse.EnsureSuccessStatusCode();
        var delivery = await confirmResponse.Content.ReadFromJsonAsync<MockWebhookDelivery>();

        var content = new StringContent(delivery!.RawBody, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook") { Content = content };
        request.Headers.Add("X-Mock-Signature", delivery.SignatureHeaderValue);
        var webhookResponse = await patientClient.SendAsync(request);
        webhookResponse.EnsureSuccessStatusCode();

        return checkout.AppointmentId;
    }

    [Fact]
    public async Task CancelAndRefund_ForScheduledPaidAppointment_RefundsInFullByDefaultAndReleasesTheAppointment()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, patientToken) = await CreateUserTokenAsync("Patient");
        var (adminId, adminToken) = await CreateUserTokenAsync("Admin");
        var patientClient = CreateClient(patientToken);

        var appointmentId = await BookAndScheduleAppointmentAsync(patientClient, doctorId, slotStartUtc, slotEndUtc);

        var cancelResponse = await CreateClient(adminToken).PostAsJsonAsync($"/api/v1/admin/appointments/{appointmentId}/cancel", new CancelAppointmentDto("No longer needed"));

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancellation = await cancelResponse.Content.ReadFromJsonAsync<AppointmentCancellationDto>();
        Assert.NotNull(cancellation);
        Assert.Equal(AppointmentStatus.Refunded, cancellation!.AppointmentStatus);
        Assert.Equal(200m, cancellation.RefundAmount);
        Assert.Equal(RefundStatus.Succeeded, cancellation.RefundStatus);

        await using var db = CreateDb();
        var appointment = await db.Appointments.FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Refunded, appointment.Status);

        var refund = await db.Refunds.FirstAsync(r => r.AppointmentId == appointmentId);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(200m, refund.Amount);
        Assert.False(string.IsNullOrWhiteSpace(refund.ProviderRefundId));

        var cancelHistoryExists = await db.AppointmentStatusHistories.AnyAsync(h =>
            h.AppointmentId == appointmentId && h.ToStatus == AppointmentStatus.CancelledByAdmin && h.ChangedByUserId == adminId);
        Assert.True(cancelHistoryExists);
    }

    [Fact]
    public async Task CancelAndRefund_WithPartialPercentage_RefundsOnlyThatFraction()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, patientToken) = await CreateUserTokenAsync("Patient");
        var (_, adminToken) = await CreateUserTokenAsync("Admin");
        var patientClient = CreateClient(patientToken);

        var appointmentId = await BookAndScheduleAppointmentAsync(patientClient, doctorId, slotStartUtc, slotEndUtc);

        var cancelResponse = await CreateClient(adminToken).PostAsJsonAsync($"/api/v1/admin/appointments/{appointmentId}/cancel", new CancelAppointmentDto("Late cancellation", RefundPercentage: 50));

        var cancellation = await cancelResponse.Content.ReadFromJsonAsync<AppointmentCancellationDto>();
        Assert.Equal(100m, cancellation!.RefundAmount);
    }

    [Fact]
    public async Task Cancel_ForAppointmentNotScheduled_ReturnsConflict()
    {
        var (doctorId, slotStartUtc, slotEndUtc) = await SeedDoctorWithScheduleAsync();
        var (_, patientToken) = await CreateUserTokenAsync("Patient");
        var (_, adminToken) = await CreateUserTokenAsync("Admin");
        var patientClient = CreateClient(patientToken);
        var adminClient = CreateClient(adminToken);

        var appointmentId = await BookAndScheduleAppointmentAsync(patientClient, doctorId, slotStartUtc, slotEndUtc);
        (await adminClient.PostAsJsonAsync($"/api/v1/admin/appointments/{appointmentId}/cancel", new CancelAppointmentDto("first cancel"))).EnsureSuccessStatusCode();

        var secondCancel = await adminClient.PostAsJsonAsync($"/api/v1/admin/appointments/{appointmentId}/cancel", new CancelAppointmentDto("second cancel"));

        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);
    }

    [Fact]
    public async Task Cancel_ForUnknownAppointment_ReturnsNotFound()
    {
        var (_, adminToken) = await CreateUserTokenAsync("Admin");

        var response = await CreateClient(adminToken).PostAsJsonAsync($"/api/v1/admin/appointments/{Guid.NewGuid()}/cancel", new CancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AsNonAdmin_ReturnsForbidden()
    {
        var (_, patientToken) = await CreateUserTokenAsync("Patient");

        var response = await CreateClient(patientToken).PostAsJsonAsync($"/api/v1/admin/appointments/{Guid.NewGuid()}/cancel", new CancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateClient().PostAsJsonAsync($"/api/v1/admin/appointments/{Guid.NewGuid()}/cancel", new CancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
