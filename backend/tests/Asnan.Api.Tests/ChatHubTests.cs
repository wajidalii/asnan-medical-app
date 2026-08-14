using Asnan.Api.Hubs;
using Asnan.Application.Auth;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Api.Tests;

/// <summary>
/// Integration tests for ChatHub (issue #27) — real MySQL, real JWT auth,
/// a real SignalR client talking to the app over TestServer's WebSocket
/// support. Covers the issue's testing requirement: authorized join
/// succeeds, unauthorized join rejected, unauthenticated connection rejected.
/// </summary>
[Collection("Database")]
public class ChatHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public ChatHubTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private async Task<(Guid ConversationId, Guid PatientUserId, Guid DoctorUserId)> SeedScheduledConversationAsync()
    {
        await using var db = CreateDb();
        var doctorUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        var patientUser = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
        db.Users.AddRange(doctorUser, patientUser);

        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Chat Test", ConsultationFee = 100m, Currency = "USD", TimeZoneId = "UTC" };
        db.DoctorProfiles.Add(doctor);

        var slotStartUtc = DateTime.UtcNow.AddDays(5);
        var appointment = new Appointment
        {
            DoctorProfileId = doctor.Id,
            PatientUserId = patientUser.Id,
            SlotStartUtc = slotStartUtc,
            SlotEndUtc = slotStartUtc.AddMinutes(30),
            Status = AppointmentStatus.Scheduled,
            ConsultationFee = 100m,
            Currency = "USD",
            SourceHoldId = Guid.NewGuid(),
        };
        db.Appointments.Add(appointment);

        var conversation = new ChatConversation { AppointmentId = appointment.Id };
        db.ChatConversations.Add(conversation);
        db.ChatParticipants.Add(new ChatParticipant { ChatConversation = conversation, UserId = patientUser.Id });
        db.ChatParticipants.Add(new ChatParticipant { ChatConversation = conversation, UserId = doctorUser.Id });

        await db.SaveChangesAsync();

        return (conversation.Id, patientUser.Id, doctorUser.Id);
    }

    private string CreateToken(Guid userId, string role = "Patient")
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(userId, new[] { role });
        return token;
    }

    private HubConnection CreateConnection(string? accessToken)
    {
        var client = _factory.CreateClient();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var wsClient = _factory.Server.CreateWebSocketClient();
                    return await wsClient.ConnectAsync(context.Uri, cancellationToken);
                };
                if (accessToken is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            })
            .Build();
    }

    [Fact]
    public async Task JoinConversation_AsThePatientParticipant_Succeeds()
    {
        var (conversationId, patientUserId, _) = await SeedScheduledConversationAsync();
        var token = CreateToken(patientUserId);
        var connection = CreateConnection(token);

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);
            // No exception thrown = joined successfully.
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task JoinConversation_AsTheDoctorParticipant_Succeeds()
    {
        var (conversationId, _, doctorUserId) = await SeedScheduledConversationAsync();
        var token = CreateToken(doctorUserId, "Doctor");
        var connection = CreateConnection(token);

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task JoinConversation_AsAStrangerNotOnTheConversation_IsRejected()
    {
        var (conversationId, _, _) = await SeedScheduledConversationAsync();
        Guid strangerId;
        await using (var db = CreateDb())
        {
            var stranger = new User { Email = $"{Guid.NewGuid()}@test.local", PasswordHash = "irrelevant" };
            db.Users.Add(stranger);
            await db.SaveChangesAsync();
            strangerId = stranger.Id;
        }
        var token = CreateToken(strangerId);
        var connection = CreateConnection(token);

        try
        {
            await connection.StartAsync();
            var exception = await Assert.ThrowsAsync<HubException>(
                () => connection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId));
            Assert.Contains("not authorized", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Connect_WithoutAToken_IsRejected()
    {
        var connection = CreateConnection(null);

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
