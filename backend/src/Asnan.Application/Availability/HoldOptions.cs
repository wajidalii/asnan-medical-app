namespace Asnan.Application.Availability;

public class HoldOptions
{
    public const string SectionName = "Hold";

    public int TtlMinutes { get; set; } = 5;
}
