using System.Security.Cryptography;
using Asnan.Application.Auth;

namespace Asnan.Infrastructure.Auth;

/// <summary>
/// Format: "v1.{iterations}.{saltBase64}.{hashBase64}". The version segment
/// means a future algorithm/iteration-count change (e.g. bumping iterations
/// as hardware gets faster) doesn't invalidate hashes already in the
/// database — <see cref="Verify"/> reads the parameters from the stored
/// hash itself rather than assuming the current defaults.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int DefaultIterations = 210_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return $"{Version}.{DefaultIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != Version)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
