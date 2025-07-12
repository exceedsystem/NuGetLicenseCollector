namespace NuGetLicenseCollector.Models;

public class PackageInfo
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string LicenseType { get; set; } = string.Empty;

    public string LicenseUrl { get; set; } = string.Empty;

    public string ProjectUrl { get; set; } = string.Empty;

    public string LicenseText { get; set; } = string.Empty;
}