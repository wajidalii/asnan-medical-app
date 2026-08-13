namespace Asnan.Application.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = null!;

    public string Audience { get; set; } = null!;

    public string SigningKey { get; set; } = null!;

    /// <summary>Short-lived by design — see ARCHITECTURE.md §4.3.</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Extended on every rotation ("reset session on app open" — §4.3). This is
    /// the per-token expiry; #9 applies it on each refresh.
    /// </summary>
    public int RefreshTokenSlidingExpiryDays { get; set; } = 30;

    /// <summary>
    /// Independent of sliding renewals — bounds a session's total lifetime no
    /// matter how often the app is opened. Stored on the UserSession, not the
    /// individual token.
    /// </summary>
    public int RefreshTokenAbsoluteExpiryDays { get; set; } = 90;
}
