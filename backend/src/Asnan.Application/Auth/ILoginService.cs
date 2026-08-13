namespace Asnan.Application.Auth;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(
        string identifier,
        string password,
        string deviceId,
        string? deviceName,
        CancellationToken cancellationToken = default);
}
