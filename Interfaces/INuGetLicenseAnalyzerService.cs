using NuGetLicenseCollector.Models;

namespace NuGetLicenseCollector.Interfaces;

public interface INuGetLicenseAnalyzerService
{
    /// <summary>
    /// Retrieves package information for a collection of package names
    /// </summary>
    /// <param name="packageNames">List of package names to analyze (format: "PackageName" or "PackageName/Version")</param>
    /// <returns>List of PackageInfo objects containing license and metadata information</returns>
    Task<List<PackageInfo>> GetPackageInfoAsync(List<string> packageNames);

    /// <summary>
    /// Disposes of resources used by the analyzer
    /// </summary>
    void Dispose();
}