using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Asnan.Application.Auth;
using Asnan.Application.Profile;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Asnan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Asnan.Api.Tests;

/// <summary>
/// HTTP-level integration tests for the caller's own profile/account —
/// issue #33. Covers the issue's explicit testing requirement: CRUD,
/// unauthorized-access rejection, and rejecting a malicious/mismatched
/// file upload (an executable renamed to .jpg).
/// </summary>
[Collection("Database")]
public class UsersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly WebApplicationFactory<Program> _factory;

    public UsersControllerTests(DatabaseFixture dbFixture, WebApplicationFactory<Program> factory)
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

    private static byte[] ValidJpegBytes()
    {
        using var bitmap = new SKBitmap(120, 90);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    [Fact]
    public async Task GetProfile_NeverSaved_ReturnsADefaultProfileWithIdentityFieldsPopulated()
    {
        var email = $"{Guid.NewGuid()}@test.local";
        await using var db = CreateDb();
        var user = new User { Email = email, PasswordHash = "irrelevant" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { "Patient" });

        var response = await CreateClient(token).GetAsync("/api/v1/users/me/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PatientProfileDto>();
        Assert.Equal(email, dto!.Email);
        Assert.Equal(string.Empty, dto.FullName);
        Assert.False(dto.HasPhoto);
    }

    [Fact]
    public async Task UpdateProfile_CreatesOnFirstSaveAndPersists()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);
        var dto = new UpdatePatientProfileDto("Jane Patient", new DateOnly(1990, 5, 1), Gender.Female, "555-0100", "1 Health St", "John Patient", "555-0101");

        var putResponse = await client.PutAsJsonAsync("/api/v1/users/me/profile", dto);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/users/me/profile");
        var saved = await getResponse.Content.ReadFromJsonAsync<PatientProfileDto>();
        Assert.Equal("Jane Patient", saved!.FullName);
        Assert.Equal(new DateOnly(1990, 5, 1), saved.DateOfBirth);
        Assert.Equal(Gender.Female, saved.Gender);
        Assert.Equal("1 Health St", saved.AddressLine);
        Assert.Equal("John Patient", saved.EmergencyContactName);
    }

    [Fact]
    public async Task UpdateProfile_EmptyFullName_ReturnsBadRequest()
    {
        var (_, token) = await CreateUserTokenAsync();
        var dto = new UpdatePatientProfileDto("", null, null, null, null, null, null);

        var response = await CreateClient(token).PutAsJsonAsync("/api/v1/users/me/profile", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_FutureDateOfBirth_ReturnsBadRequest()
    {
        var (_, token) = await CreateUserTokenAsync();
        var dto = new UpdatePatientProfileDto("Jane Patient", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null, null, null, null, null);

        var response = await CreateClient(token).PutAsJsonAsync("/api/v1/users/me/profile", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_ValidImage_SucceedsAndIsRetrievable()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ValidJpegBytes());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "photo.png");

        var uploadResponse = await client.PostAsync("/api/v1/users/me/profile/photo", content);
        Assert.Equal(HttpStatusCode.NoContent, uploadResponse.StatusCode);

        var profile = await (await client.GetAsync("/api/v1/users/me/profile")).Content.ReadFromJsonAsync<PatientProfileDto>();
        Assert.True(profile!.HasPhoto);

        var photoResponse = await client.GetAsync("/api/v1/users/me/profile/photo");
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        Assert.Equal("image/jpeg", photoResponse.Content.Headers.ContentType!.MediaType);
        var bytes = await photoResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task UploadPhoto_ExecutableRenamedToJpg_IsRejected()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);

        // A fake PE-header prefix — not decodable as any real image format regardless of the claimed extension/content-type.
        var maliciousBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(maliciousBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "totally-a-photo.jpg");

        var response = await client.PostAsync("/api/v1/users/me/profile/photo", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var profile = await (await client.GetAsync("/api/v1/users/me/profile")).Content.ReadFromJsonAsync<PatientProfileDto>();
        Assert.False(profile!.HasPhoto);
    }

    [Fact]
    public async Task UploadPhoto_OversizedFile_IsRejected()
    {
        var (_, token) = await CreateUserTokenAsync();
        var client = CreateClient(token);

        var oversized = new byte[6 * 1024 * 1024]; // over the 5MB default
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(oversized);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "huge.jpg");

        var response = await client.PostAsync("/api/v1/users/me/profile/photo", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPhoto_NoneUploaded_ReturnsNotFound()
    {
        var (_, token) = await CreateUserTokenAsync();

        var response = await CreateClient(token).GetAsync("/api/v1/users/me/profile/photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact]
    public async Task RequestAccountDeletion_RevokesSessionsAndBlocksFutureLogin()
    {
        var email = $"{Guid.NewGuid()}@test.local";
        const string password = "correct horse battery staple 1";

        using var scope = _factory.Services.CreateScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        await using var db = CreateDb();
        var user = new User { Email = email, PasswordHash = passwordHasher.Hash(password) };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleIds.Patient });
        await db.SaveChangesAsync();

        var (token, _) = jwtService.GenerateAccessToken(user.Id, new[] { "Patient" });

        var deleteResponse = await CreateClient(token).DeleteAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var loginResponse = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Identifier = email, Password = password, DeviceId = "test-device", DeviceName = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }
}
