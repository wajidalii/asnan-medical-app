using System.Security.Cryptography;

namespace Asnan.Application.Common;

/// <summary>
/// Generates and hashes the random high-entropy tokens used anywhere a
/// "possession of this value proves something" credential is needed
/// (signup tokens now; refresh tokens land on the same pattern in #9).
/// Plain SHA-256 is sufficient here — unlike OTP codes, these tokens have
/// 256 bits of entropy, so unkeyed hashing isn't meaningfully brute-forceable.
/// </summary>
public static class OpaqueTokenGenerator
{
    public static string Generate(int sizeBytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(sizeBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
