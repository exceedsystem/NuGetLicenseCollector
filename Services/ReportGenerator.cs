using System.Text;
using System.Text.Json;
using NuGetLicenseCollector.Interfaces;
using NuGetLicenseCollector.Models;

namespace NuGetLicenseCollector.Services;

public class ReportGenerator : IReportGenerator
{
    public async Task GenerateReportAsync(List<PackageInfo> packages, string outputPath)
    {
        var report = new StringBuilder();

        report.AppendLine("NuGet Package License Report");
        report.AppendLine("================================");
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Total packages: {packages.Count}");
        report.AppendLine();

        report.AppendLine("License Summary:");
        report.AppendLine("================");

        var licenseGroups = packages.GroupBy(p => p.LicenseType).OrderBy(g => g.Key);
        foreach (var group in licenseGroups)
        {
            report.AppendLine($"{group.Key}: {group.Count()} packages");
            foreach (var package in group.OrderBy(p => p.Name))
            {
                report.AppendLine($"  - {package.Name} ({package.Version})");
            }
            report.AppendLine();
        }

        report.AppendLine();

        var sortedPackages = packages.OrderBy(p => p.Name).ToList();

        for (int i = 0; i < sortedPackages.Count; i++)
        {
            var package = sortedPackages[i];

            report.AppendLine("################################################################################");
            report.AppendLine($"# {package.Name} (v{package.Version})");
            report.AppendLine("################################################################################");

            report.AppendLine($"Author: {package.Author}");
            report.AppendLine($"License Type: {package.LicenseType}");

            if (!string.IsNullOrEmpty(package.ProjectUrl))
            {
                report.AppendLine($"Project URL: {package.ProjectUrl}");
            }

            if (!string.IsNullOrEmpty(package.LicenseUrl))
            {
                report.AppendLine($"License URL: {package.LicenseUrl}");
            }

            if (!string.IsNullOrEmpty(package.LicenseText))
            {
                report.AppendLine();
                report.AppendLine(">>> START OF LICENSE TEXT <<<");
                report.AppendLine(package.LicenseText);
                report.AppendLine(">>> END OF LICENSE TEXT <<<");
            }

            report.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, report.ToString(), Encoding.UTF8);
        Console.WriteLine($"Report generated: {outputPath}");
    }

    public async Task GenerateJsonReportAsync(List<PackageInfo> packages, string outputPath)
    {
        var reportData = new
        {
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalPackages = packages.Count,
            Packages = packages.OrderBy(p => p.Name).ToList(),
            LicenseSummary = packages.GroupBy(p => p.LicenseType)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    LicenseType = g.Key,
                    PackageCount = g.Count(),
                    Packages = g.OrderBy(p => p.Name).Select(p => new
                    {
                        p.Name,
                        p.Version
                    }).ToList()
                }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(reportData, options);
        await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);
        Console.WriteLine($"JSON report generated: {outputPath}");
    }
}