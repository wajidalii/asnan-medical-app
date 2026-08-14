using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Asnan.Application.Appointments;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Application.Common;
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
/// HTTP-level integration tests for appointment listing + self-service
/// cancellation (issue #24) — real controller, real MySQL, real mock
/// provider, covering the issue's testing requirement: listing pagination,
/// unauthorized-access rejection, cancellation-triggering-refund, and
/// cancellation-policy window-boundary edge cases.
/// </summary>
[Collection("Database")]
public class AppointmentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public AppointmentsControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid DoctorId, Guid DoctorUserId)> SeedDoctorWithScheduleAsync(DateOnly date)
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(doctorUser);

        var doctor = new DoctorProfile
        {
            UserId = doctorUser.Id,
            FullName = "Dr. Appointments Test",
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

    private async Task<(Guid UserId, string Token)> CreateUserTokenAsync(string role)
    {
        await using var db = CreateDb();
        var user = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.Add(user);
        var roleId = role switch { "Admin" => RoleIds.Admin, "Doctor" => RoleIds.Doctor, _ => RoleIds.Patient };
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
    public async Task List_ReturnsOnlyTheCallersOwnAppointments()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);

        var (_, ownerToken) = await CreateUserTokenAsync("Patient");
        var (_, strangerToken) = await CreateUserTokenAsync("Patient");
        await BookAndScheduleAppointmentAsync(CreateClient(ownerToken), doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));

        var ownerList = await (await CreateClient(ownerToken).GetAsync("/api/v1/appointments?scope=Upcoming")).Content.ReadFromJsonAsync<PagedResult<AppointmentSummaryDto>>();
        var strangerList = await (await CreateClient(strangerToken).GetAsync("/api/v1/appointments?scope=Upcoming")).Content.ReadFromJsonAsync<PagedResult<AppointmentSummaryDto>>();

        Assert.Contains(ownerList!.Items, a => a.DoctorProfileId == doctorId);
        Assert.DoesNotContain(strangerList!.Items, a => a.DoctorProfileId == doctorId);

        // The mobile chat feature (#29) navigates via this id — must be populated once Scheduled.
        var owned = ownerList.Items.Single(a => a.DoctorProfileId == doctorId);
        Assert.NotNull(owned.ChatConversationId);
    }

    [Fact]
    public async Task List_DoctorSeesTheirOwnAppointmentAsWell()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(41));
        var (doctorId, doctorUserId) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);

        var (_, patientToken) = await CreateUserTokenAsync("Patient");
        var appointmentId = await BookAndScheduleAppointmentAsync(CreateClient(patientToken), doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));

        await using var db = CreateDb();
        var doctorJwt = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (doctorToken, _) = doctorJwt.GenerateAccessToken(doctorUserId, new[] { "Doctor" });

        var doctorList = await (await CreateClient(doctorToken).GetAsync("/api/v1/appointments?scope=Upcoming")).Content.ReadFromJsonAsync<PagedResult<AppointmentSummaryDto>>();

        Assert.Contains(doctorList!.Items, a => a.Id == appointmentId);
    }

    [Fact]
    public async Task List_RespectsPagination()
    {
        var (_, token) = await CreateUserTokenAsync("Patient");
        var client = CreateClient(token);
        var doctorIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45 + i));
            var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
            var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
            await BookAndScheduleAppointmentAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));
            doctorIds.Add(doctorId);
        }

        var page1 = await (await client.GetAsync("/api/v1/appointments?scope=Upcoming&page=1&pageSize=2")).Content.ReadFromJsonAsync<PagedResult<AppointmentSummaryDto>>();
        var page2 = await (await client.GetAsync("/api/v1/appointments?scope=Upcoming&page=2&pageSize=2")).Content.ReadFromJsonAsync<PagedResult<AppointmentSummaryDto>>();

        Assert.Equal(2, page1!.Items.Count);
        Assert.Single(page2!.Items);
        Assert.Equal(3, page1.TotalCount);
        Assert.Empty(page1.Items.Select(a => a.Id).Intersect(page2.Items.Select(a => a.Id)));
    }

    [Fact]
    public async Task Cancel_ByOwningPatient_WellInAdvance_RefundsInFull()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (patientId, token) = await CreateUserTokenAsync("Patient");
        var client = CreateClient(token);

        var appointmentId = await BookAndScheduleAppointmentAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));

        var response = await client.PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new RequestCancelAppointmentDto("Change of plans"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AppointmentCancellationDto>();
        Assert.Equal(AppointmentStatus.Refunded, result!.AppointmentStatus);
        Assert.Equal(100m, result.RefundAmount);

        await using var db = CreateDb();
        var historyExists = await db.AppointmentStatusHistories.AnyAsync(h =>
            h.AppointmentId == appointmentId && h.ToStatus == AppointmentStatus.CancelledByPatient && h.ChangedByUserId == patientId && h.Reason == "Change of plans");
        Assert.True(historyExists);
    }

    [Fact]
    public async Task PreviewCancellation_WellInAdvance_ShowsFullRefundAllowed()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(53));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, token) = await CreateUserTokenAsync("Patient");
        var client = CreateClient(token);
        var appointmentId = await BookAndScheduleAppointmentAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));

        var response = await client.GetAsync($"/api/v1/appointments/{appointmentId}/cancellation-preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewDto>();
        Assert.True(preview!.IsAllowed);
        Assert.Equal(100, preview.RefundPercentage);
        Assert.Equal(100m, preview.RefundAmount);

        await using var db = CreateDb();
        var appointment = await db.Appointments.FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status); // preview must not mutate anything
    }

    [Fact]
    public async Task PreviewCancellation_TooCloseToTheAppointment_ShowsNotAllowedWithoutErroring()
    {
        Guid appointmentId;
        string token;
        await using (var db = CreateDb())
        {
            var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            db.Users.AddRange(doctorUser, patientUser);
            db.UserRoles.Add(new UserRole { UserId = patientUser.Id, RoleId = RoleIds.Patient });

            var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Soon Preview", ConsultationFee = 60m, Currency = "USD", TimeZoneId = "UTC" };
            db.DoctorProfiles.Add(doctor);

            var slotStartUtc = DateTime.UtcNow.AddMinutes(30);
            var appointment = new Appointment
            {
                DoctorProfileId = doctor.Id,
                PatientUserId = patientUser.Id,
                SlotStartUtc = slotStartUtc,
                SlotEndUtc = slotStartUtc.AddMinutes(30),
                Status = AppointmentStatus.Scheduled,
                ConsultationFee = 60m,
                Currency = "USD",
                SourceHoldId = Guid.NewGuid(),
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            appointmentId = appointment.Id;
            var jwtService = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IJwtTokenService>();
            (token, _) = jwtService.GenerateAccessToken(patientUser.Id, new[] { "Patient" });
        }

        var response = await CreateClient(token).GetAsync($"/api/v1/appointments/{appointmentId}/cancellation-preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewDto>();
        Assert.False(preview!.IsAllowed);
    }

    [Fact]
    public async Task Cancel_ByNonParticipant_ReturnsForbidden()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(51));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, ownerToken) = await CreateUserTokenAsync("Patient");
        var (_, strangerToken) = await CreateUserTokenAsync("Patient");

        var appointmentId = await BookAndScheduleAppointmentAsync(CreateClient(ownerToken), doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));

        var response = await CreateClient(strangerToken).PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new RequestCancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ForUnknownAppointment_ReturnsNotFound()
    {
        var (_, token) = await CreateUserTokenAsync("Patient");

        var response = await CreateClient(token).PostAsJsonAsync($"/api/v1/appointments/{Guid.NewGuid()}/cancel", new RequestCancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsConflict()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(52));
        var (doctorId, _) = await SeedDoctorWithScheduleAsync(date);
        var slotStartUtc = new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        var (_, token) = await CreateUserTokenAsync("Patient");
        var client = CreateClient(token);
        var appointmentId = await BookAndScheduleAppointmentAsync(client, doctorId, slotStartUtc, slotStartUtc.AddMinutes(30));
        (await client.PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new RequestCancelAppointmentDto(null))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new RequestCancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_TooCloseToTheAppointment_ReturnsConflictAndDoesNotCancel()
    {
        // Seeded directly rather than via the real booking flow — the window-closed
        // case needs a Scheduled appointment starting within the next hour, which
        // the doctor-schedule/hold/checkout machinery isn't set up to produce cleanly.
        Guid appointmentId;
        Guid patientUserId;
        string token;
        await using (var db = CreateDb())
        {
            var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            db.Users.AddRange(doctorUser, patientUser);
            db.UserRoles.Add(new UserRole { UserId = patientUser.Id, RoleId = RoleIds.Patient });

            var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Soon", ConsultationFee = 80m, Currency = "USD", TimeZoneId = "UTC" };
            db.DoctorProfiles.Add(doctor);

            var slotStartUtc = DateTime.UtcNow.AddMinutes(30);
            var appointment = new Appointment
            {
                DoctorProfileId = doctor.Id,
                PatientUserId = patientUser.Id,
                SlotStartUtc = slotStartUtc,
                SlotEndUtc = slotStartUtc.AddMinutes(30),
                Status = AppointmentStatus.Scheduled,
                ConsultationFee = 80m,
                Currency = "USD",
                SourceHoldId = Guid.NewGuid(),
            };
            db.Appointments.Add(appointment);

            db.PaymentTransactions.Add(new PaymentTransaction
            {
                AppointmentId = appointment.Id,
                ProviderSessionId = "sess",
                ProviderTransactionId = "txn",
                RedirectUrl = "/x",
                IdempotencyKey = Guid.NewGuid().ToString(),
                Amount = 80m,
                Currency = "USD",
                Status = PaymentTransactionStatus.Succeeded,
            });

            await db.SaveChangesAsync();

            appointmentId = appointment.Id;
            patientUserId = patientUser.Id;

            var jwtService = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IJwtTokenService>();
            (token, _) = jwtService.GenerateAccessToken(patientUserId, new[] { "Patient" });
        }

        var response = await CreateClient(token).PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new RequestCancelAppointmentDto(null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var verifyDb = CreateDb();
        var appointment2 = await verifyDb.Appointments.FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment2.Status);
    }
}
