namespace Asnan.Application.Auth;

/// <summary>
/// Rejects the most obviously-guessable passwords per ARCHITECTURE.md §4.1's
/// NIST-800-63B-inspired policy (length + breach/common-list check, not
/// forced character-class composition rules).
///
/// This is a small curated list, not a full top-10k breach corpus — embedding
/// one wholesale wasn't worth the line count for what this scaffold needs to
/// demonstrate; the mechanism (check against a swappable list before
/// accepting a password) is what matters, and the list is trivial to grow or
/// replace with a real breached-password corpus later without touching any
/// caller.
/// </summary>
public static class CommonPasswordChecker
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "123456", "123456789", "12345678",
        "12345", "1234567", "1234567890", "qwerty", "qwerty123", "qwertyuiop",
        "letmein", "welcome", "welcome1", "monkey", "dragon", "master", "football",
        "baseball", "basketball", "superman", "batman", "trustno1", "iloveyou",
        "admin", "administrator", "abc123", "abcd1234", "asdfghjkl", "asdf1234",
        "1q2w3e4r", "zaq12wsx", "changeme", "passw0rd", "p@ssword", "p@ssw0rd",
        "sunshine", "princess", "starwars", "shadow", "michael", "jennifer",
        "computer", "internet", "letmein123", "login", "loginpassword",
        "whatever", "freedom", "ninja", "mustang", "access", "flower",
        "hottie", "loveme", "jesus", "hunter", "buster", "soccer", "harley",
        "ranger", "daniel", "matthew", "andrew", "joshua", "michelle",
        "charlie", "andrea", "12341234", "11111111", "00000000", "88888888",
        "123123123", "qazwsx", "qazwsxedc", "1qaz2wsx", "aaaaaaaa", "121212",
        "112233", "123321", "666666", "888888", "654321", "222222", "999999",
        "7777777", "1qaz@wsx", "iloveyou1", "myspace1", "blink182", "pokemon",
        "yankees", "chicago", "phoenix", "tigger", "cheese", "summer", "winter",
        "spring", "autumn", "december", "january", "orange", "purple",
    };

    public static bool IsCommon(string password) => CommonPasswords.Contains(password);
}
