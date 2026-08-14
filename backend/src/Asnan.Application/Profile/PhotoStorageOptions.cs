namespace Asnan.Application.Profile;

public class PhotoStorageOptions
{
    public const string SectionName = "PhotoStorage";

    /// <summary>Relative to the app's content root (resolved by LocalPatientPhotoService) — deliberately outside wwwroot, never directly web-servable (ARCHITECTURE.md §13).</summary>
    public string RootPath { get; set; } = "App_Data/patient-photos";

    public int MaxSizeBytes { get; set; } = 5 * 1024 * 1024;
}
