namespace Asnan.Domain.Enums;

/// <summary>Optional — prompt.md's "User Profile": "gender where required." Never used to gate any feature, purely a self-reported profile field.</summary>
public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
    PreferNotToSay = 4,
}
