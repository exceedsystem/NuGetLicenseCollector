using NuGetLicenseCollector.Models;

namespace NuGetLicenseCollector.Interfaces;

public interface IReportGenerator
{
    Task GenerateReportAsync(List<PackageInfo> packages, string outputPath);
    Task GenerateJsonReportAsync(List<PackageInfo> packages, string outputPath);
}