using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asnan.Api.Hubs;
using Asnan.Application.Auth;
using Asnan.Application.Chat;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asnan.Api.Tests;

/// <summary>
/// Integration tests for chat message send/receive, pagination, read
/// receipts, and durability (issue #28) — real MySQL, real JWT auth, a real
/// SignalR client. Covers the issue's testing requirement directly.
/// </summary>
[Collection("Database")]
public class ChatMessagingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public ChatMessagingTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

        var doctor = new DoctorProfile { UserId = doctorUser.Id, FullName = "Dr. Messaging Test", ConsultationFee = 100m, Currency = "USD", TimeZoneId = "UTC" };
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

    private HubConnection CreateConnection(WebApplicationFactory<Program> factory, string accessToken)
    {
        var client = factory.CreateClient();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var wsClient = factory.Server.CreateWebSocketClient();
                    return await wsClient.ConnectAsync(context.Uri, cancellationToken);
                };
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();
    }

    private HttpClient CreateApiClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task SendMessage_PersistsAndBroadcastsToTheOtherParticipant()
    {
        var (conversationId, patientUserId, doctorUserId) = await SeedScheduledConversationAsync();
        var patientConnection = CreateConnection(_factory, CreateToken(patientUserId));
        var doctorConnection = CreateConnection(_factory, CreateToken(doctorUserId, "Doctor"));

        try
        {
            await patientConnection.StartAsync();
            await doctorConnection.StartAsync();
            await patientConnection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);
            await doctorConnection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);

            var received = new TaskCompletionSource<ChatMessageDto>();
            doctorConnection.On<ChatMessageDto>("ReceiveMessage", msg => received.TrySetResult(msg));

            await patientConnection.InvokeAsync(nameof(ChatHub.SendMessage), conversationId, "Hello doctor");

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Equal(received.Task, completed);
            var receivedMessage = await received.Task;
            Assert.Equal("Hello doctor", receivedMessage.Content);
            Assert.Equal(patientUserId, receivedMessage.SenderUserId);

            await using var db = CreateDb();
            var stored = await db.ChatMessages.SingleAsync(m => m.ChatConversationId == conversationId);
            Assert.Equal("Hello doctor", stored.Content);
            Assert.Equal(patientUserId, stored.SenderUserId);
        }
        finally
        {
            await patientConnection.DisposeAsync();
            await doctorConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task SendMessage_ToAnOfflineRecipient_StillPersistsAndFiresTheOfflineNotifyHook()
    {
        var (conversationId, patientUserId, doctorUserId) = await SeedScheduledConversationAsync();

        var calls = new List<(Guid RecipientUserId, Guid ConversationId, Guid MessageId)>();
        var fakeNotifier = new FakeOfflineMessageNotifier(calls);
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IOfflineMessageNotifier>(fakeNotifier))));

        var patientConnection = CreateConnection(factory, CreateToken(patientUserId));

        try
        {
            await patientConnection.StartAsync();
            await patientConnection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);
            // Doctor never connects — recipient is offline for this send.

            await patientConnection.InvokeAsync(nameof(ChatHub.SendMessage), conversationId, "Are you there?");

            await using var db = CreateDb();
            var stored = await db.ChatMessages.SingleAsync(m => m.ChatConversationId == conversationId);
            Assert.Equal("Are you there?", stored.Content);

            Assert.Single(calls);
            Assert.Equal(doctorUserId, calls[0].RecipientUserId);
            Assert.Equal(stored.Id, calls[0].MessageId);
        }
        finally
        {
            await patientConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task SendMessage_WithEmptyContent_IsRejected()
    {
        var (conversationId, patientUserId, _) = await SeedScheduledConversationAsync();
        var connection = CreateConnection(_factory, CreateToken(patientUserId));

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);

            await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(nameof(ChatHub.SendMessage), conversationId, ""));
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetMessages_ReturnsCorrectlyPaginatedHistoryOldestFirst()
    {
        var (conversationId, patientUserId, doctorUserId) = await SeedScheduledConversationAsync();

        await using (var db = CreateDb())
        {
            var baseTime = DateTime.UtcNow.AddMinutes(-10);
            for (var i = 0; i < 5; i++)
            {
                db.ChatMessages.Add(new ChatMessage
                {
                    ChatConversationId = conversationId,
                    SenderUserId = i % 2 == 0 ? patientUserId : doctorUserId,
                    Content = $"message-{i}",
                    SentAtUtc = baseTime.AddMinutes(i),
                });
            }
            await db.SaveChangesAsync();
        }

        var client = CreateApiClient(CreateToken(patientUserId));

        var firstPageResponse = await client.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages?pageSize=3");
        firstPageResponse.EnsureSuccessStatusCode();
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<ChatMessagePageDto>();

        Assert.NotNull(firstPage);
        Assert.Equal(3, firstPage!.Messages.Count);
        Assert.True(firstPage.HasMore);
        // Newest-3 of 5, returned oldest-first: message-2, message-3, message-4.
        Assert.Equal(["message-2", "message-3", "message-4"], firstPage.Messages.Select(m => m.Content));

        var secondPageResponse = await client.GetAsync(
            $"/api/v1/chat/conversations/{conversationId}/messages?pageSize=3&before={Uri.EscapeDataString(firstPage.NextBeforeCursor!.Value.ToString("O"))}");
        secondPageResponse.EnsureSuccessStatusCode();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<ChatMessagePageDto>();

        Assert.NotNull(secondPage);
        Assert.False(secondPage!.HasMore);
        Assert.Equal(["message-0", "message-1"], secondPage.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task GetMessages_ForANonParticipant_ReturnsForbidden()
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

        var response = await CreateApiClient(CreateToken(strangerId)).GetAsync($"/api/v1/chat/conversations/{conversationId}/messages");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMessages_ForAnUnknownConversation_ReturnsNotFound()
    {
        var (_, patientUserId, _) = await SeedScheduledConversationAsync();

        var response = await CreateApiClient(CreateToken(patientUserId)).GetAsync($"/api/v1/chat/conversations/{Guid.NewGuid()}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_UpdatesReadStatusAndUnreadCountBecomesZero()
    {
        var (conversationId, patientUserId, doctorUserId) = await SeedScheduledConversationAsync();

        Guid lastMessageId;
        await using (var db = CreateDb())
        {
            var message = new ChatMessage { ChatConversationId = conversationId, SenderUserId = doctorUserId, Content = "hi", SentAtUtc = DateTime.UtcNow };
            db.ChatMessages.Add(message);
            await db.SaveChangesAsync();
            lastMessageId = message.Id;
        }

        var connection = CreateConnection(_factory, CreateToken(patientUserId));
        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync(nameof(ChatHub.JoinConversation), conversationId);
            await connection.InvokeAsync(nameof(ChatHub.MarkAsRead), conversationId, lastMessageId);
        }
        finally
        {
            await connection.DisposeAsync();
        }

        var response = await CreateApiClient(CreateToken(patientUserId)).GetAsync($"/api/v1/chat/conversations/{conversationId}/read-status");
        response.EnsureSuccessStatusCode();
        var readStatus = await response.Content.ReadFromJsonAsync<ReadStatusDto>();

        Assert.Equal(lastMessageId, readStatus!.LastReadMessageId);
        Assert.Equal(0, readStatus.UnreadCount);
    }

    [Fact]
    public async Task GetReadStatus_WithNoReadsYet_CountsAllMessagesFromTheOtherParticipantAsUnread()
    {
        var (conversationId, patientUserId, doctorUserId) = await SeedScheduledConversationAsync();

        await using (var db = CreateDb())
        {
            db.ChatMessages.Add(new ChatMessage { ChatConversationId = conversationId, SenderUserId = doctorUserId, Content = "one", SentAtUtc = DateTime.UtcNow.AddMinutes(-2) });
            db.ChatMessages.Add(new ChatMessage { ChatConversationId = conversationId, SenderUserId = doctorUserId, Content = "two", SentAtUtc = DateTime.UtcNow.AddMinutes(-1) });
            db.ChatMessages.Add(new ChatMessage { ChatConversationId = conversationId, SenderUserId = patientUserId, Content = "mine", SentAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var response = await CreateApiClient(CreateToken(patientUserId)).GetAsync($"/api/v1/chat/conversations/{conversationId}/read-status");
        response.EnsureSuccessStatusCode();
        var readStatus = await response.Content.ReadFromJsonAsync<ReadStatusDto>();

        Assert.Null(readStatus!.LastReadMessageId);
        Assert.Equal(2, readStatus.UnreadCount); // only the doctor's 2 messages count — not the patient's own.
    }

    private class FakeOfflineMessageNotifier : IOfflineMessageNotifier
    {
        public FakeOfflineMessageNotifier(List<(Guid RecipientUserId, Guid ConversationId, Guid MessageId)> calls)
        {
            _calls = calls;
        }

        private readonly List<(Guid RecipientUserId, Guid ConversationId, Guid MessageId)> _calls;

        public Task NotifyAsync(Guid recipientUserId, Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
        {
            _calls.Add((recipientUserId, conversationId, messageId));
            return Task.CompletedTask;
        }
    }
}
