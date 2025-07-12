using System.Text;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetLicenseCollector.Interfaces;
using NuGetLicenseCollector.Models;

namespace NuGetLicenseCollector.Services;

/// <summary>
/// Analyzes NuGet packages to extract license information and package metadata
/// </summary>
public class NuGetLicenseAnalyzerService : INuGetLicenseAnalyzerService
{
    private readonly ILogger _logger;
    private readonly SourceCacheContext _cache;
    private readonly List<SourceRepository> _repositories;
    private readonly ILicenseCacheService _licenseCacheService;

    /// <summary>
    /// Initializes a new instance of the NuGetLicenseAnalyzer with required dependencies
    /// </summary>
    /// <param name="licenseCacheService">Service for managing license caching operations</param>
    /// <param name="forceRefresh">If true, clears local license cache and downloads fresh license texts</param>
    public NuGetLicenseAnalyzerService(ILicenseCacheService licenseCacheService, bool forceRefresh = false)
    {
        // REQUIRED: Register code pages provider to handle various text encodings in license files
        // Some license files may use encodings other than UTF-8 (e.g., Windows-1252)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        _logger = NullLogger.Instance;
        _cache = new SourceCacheContext();
        _licenseCacheService = licenseCacheService ?? throw new ArgumentNullException(nameof(licenseCacheService));

        // Clear cache if force refresh is requested
        if (forceRefresh)
        {
            _licenseCacheService.ClearCache();
        }

        // Use official NuGet.org API v3 endpoint for maximum compatibility and reliability
        // Note: Currently hardcoded to NuGet.org - future enhancement could support custom feeds
        var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
        _repositories = new List<SourceRepository>
        {
            Repository.Factory.GetCoreV3(packageSource)
        };
    }

    /// <summary>
    /// Initializes a new instance of the NuGetLicenseAnalyzer with default cache service
    /// </summary>
    /// <param name="forceRefresh">If true, clears local license cache and downloads fresh license texts</param>
    public NuGetLicenseAnalyzerService(bool forceRefresh = false) : this(new LicenseCacheService(new HttpClient()), forceRefresh)
    {
    }

    /// <summary>
    /// Retrieves package information for a collection of package names
    /// </summary>
    /// <param name="packageNames">List of package names to analyze (format: "PackageName" or "PackageName/Version")</param>
    /// <returns>List of PackageInfo objects containing license and metadata information</returns>
    public async Task<List<PackageInfo>> GetPackageInfoAsync(List<string> packageNames)
    {
        var packages = new List<PackageInfo>();
        var processedPackages = new HashSet<string>();

        foreach (var packageName in packageNames)
        {
            // Skip duplicate packages to avoid redundant processing
            if (processedPackages.Contains(packageName))
            {
                continue;
            }

            processedPackages.Add(packageName);

            try
            {
                var package = await GetPackageInfoAsync(packageName);
                if (package != null)
                {
                    packages.Add(package);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting info for package {packageName}: {ex.Message}");
                // Create error package info when processing fails
                var parts = packageName.Split('/');
                packages.Add(new PackageInfo
                {
                    Name = parts[0],
                    Version = parts.Length > 1 ? parts[1] : "Unknown",
                    Author = "Unknown",
                    LicenseType = $"Error: {ex.Message}",
                    ProjectUrl = string.Empty
                });
            }
        }

        // Return sorted list by package name for consistent output
        return packages.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Disposes of resources used by the analyzer
    /// </summary>
    public void Dispose()
    {
        _cache?.Dispose();
    }

    /// <summary>
    /// Retrieves detailed package information for a single package
    /// </summary>
    /// <param name="packageName">Package name in format "PackageName" or "PackageName/Version"</param>
    /// <returns>PackageInfo object with license and metadata information</returns>
    private async Task<PackageInfo?> GetPackageInfoAsync(string packageName)
    {
        try
        {
            // Parse package name and version from input string
            var parts = packageName.Split('/');
            var name = parts[0];
            var version = parts.Length > 1 ? parts[1] : null;

            Console.WriteLine($"Searching for package: {name} (version: {version ?? "latest"})");

            var repository = _repositories.First();
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();

            IEnumerable<IPackageSearchMetadata> metadata = null;

            // Retry mechanism for network reliability - NuGet API can be flaky
            for (int retryCount = 0; retryCount < 3; retryCount++)
            {
                try
                {
                    // Include prerelease and unlisted packages for comprehensive analysis
                    metadata = await metadataResource.GetMetadataAsync(
                        name,
                        includePrerelease: true,
                        includeUnlisted: true,
                        _cache,
                        _logger,
                        CancellationToken.None);
                    break;
                }
                catch (Exception ex) when (retryCount < 2)
                {
                    Console.WriteLine($"Retry {retryCount + 1} for package {name}: {ex.Message}");
                    // Exponential backoff to avoid overwhelming the API
                    await Task.Delay(1000 * (retryCount + 1));
                }
            }

            if (metadata == null)
            {
                Console.WriteLine($"Failed to retrieve metadata for package {name} after 3 retries");
                return new PackageInfo
                {
                    Name = name,
                    Version = version ?? "Unknown",
                    Author = "Unknown",
                    LicenseType = "Failed to retrieve metadata",
                    ProjectUrl = string.Empty
                };
            }

            Console.WriteLine($"Found {metadata.Count()} versions for {name}");

            IPackageSearchMetadata? latestMetadata = null;

            // Find the specific version or latest version
            if (version != null)
            {
                // Try to find exact version match
                if (NuGetVersion.TryParse(version, out var targetVersion))
                {
                    latestMetadata = metadata.FirstOrDefault(m => m.Identity.Version.Equals(targetVersion));

                    // Try normalized version match if exact match fails
                    if (latestMetadata == null)
                    {
                        latestMetadata = metadata.FirstOrDefault(m =>
                            m.Identity.Version.ToNormalizedString().Equals(targetVersion.ToNormalizedString()));
                    }

                    // Try major.minor.patch match if normalized match fails
                    if (latestMetadata == null)
                    {
                        latestMetadata = metadata.FirstOrDefault(m =>
                            m.Identity.Version.Major == targetVersion.Major &&
                            m.Identity.Version.Minor == targetVersion.Minor &&
                            m.Identity.Version.Patch == targetVersion.Patch);
                    }
                }
                else
                {
                    // Fallback to string comparison if version parsing fails
                    latestMetadata = metadata.FirstOrDefault(m => m.Identity.Version.ToString() == version);
                }
            }
            else
            {
                // Get latest version when no specific version is requested
                latestMetadata = metadata.OrderByDescending(m => m.Identity.Version).FirstOrDefault();
            }

            // Handle case where specific version is not found
            if (latestMetadata == null)
            {
                Console.WriteLine($"Package {packageName} not found (searched name: {name}, version: {version})");
                if (metadata.Any())
                {
                    Console.WriteLine($"Available versions: {string.Join(", ", metadata.Select(m => m.Identity.Version.ToString()).Take(10))}");
                    // Fall back to latest version if specific version not found
                    latestMetadata = metadata.OrderByDescending(m => m.Identity.Version).FirstOrDefault();
                    if (latestMetadata != null)
                    {
                        Console.WriteLine($"Using latest version {latestMetadata.Identity.Version} instead");
                    }
                }

                if (latestMetadata == null)
                {
                    return new PackageInfo
                    {
                        Name = name,
                        Version = version ?? "Not found",
                        Author = "Unknown",
                        LicenseType = "Package not found",
                        ProjectUrl = string.Empty
                    };
                }
            }

            // Create package info object with basic metadata
            var packageInfo = new PackageInfo
            {
                Name = latestMetadata.Identity.Id,
                Version = latestMetadata.Identity.Version.ToString(),
                Author = latestMetadata.Authors ?? "Unknown",
                ProjectUrl = latestMetadata.ProjectUrl?.ToString() ?? string.Empty
            };

            // Populate license information
            await PopulateLicenseInfoAsync(packageInfo, latestMetadata);

            return packageInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing package {packageName}: {ex.Message}");
            var parts = packageName.Split('/');
            return new PackageInfo
            {
                Name = parts[0],
                Version = parts.Length > 1 ? parts[1] : "Unknown",
                Author = "Unknown",
                LicenseType = $"Error: {ex.Message}",
                ProjectUrl = string.Empty
            };
        }
    }

    /// <summary>
    /// Populates license information for a package from NuGet metadata
    /// </summary>
    /// <param name="packageInfo">Package info object to populate</param>
    /// <param name="metadata">NuGet package metadata containing license information</param>
    private async Task PopulateLicenseInfoAsync(PackageInfo packageInfo, IPackageSearchMetadata metadata)
    {
        try
        {
            if (metadata.LicenseMetadata != null)
            {
                // Handle SPDX license expression (e.g., MIT, Apache-2.0)
                if (metadata.LicenseMetadata.Type == NuGet.Packaging.LicenseType.Expression)
                {
                    packageInfo.LicenseType = metadata.LicenseMetadata.License;
                    packageInfo.LicenseText = await GetLicenseTextFromExpressionAsync(metadata.LicenseMetadata.License);
                }
                // Handle license file embedded in package
                else if (metadata.LicenseMetadata.Type == NuGet.Packaging.LicenseType.File)
                {
                    packageInfo.LicenseType = "File";
                    try
                    {
                        // Construct URL to download license file from NuGet package
                        var licenseFileUrl = new Uri($"https://api.nuget.org/v3-flatcontainer/{packageInfo.Name.ToLower()}/{packageInfo.Version.ToLower()}/{metadata.LicenseMetadata.License}");
                        packageInfo.LicenseText = await _licenseCacheService.DownloadContentAsync(licenseFileUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download license file for {packageInfo.Name}: {ex.Message}");
                        packageInfo.LicenseText = $"License file: {metadata.LicenseMetadata.License}";
                    }
                }

                // If there's also a license URL, store it and use as fallback
                if (metadata.LicenseUrl != null)
                {
                    packageInfo.LicenseUrl = metadata.LicenseUrl.ToString();

                    if (string.IsNullOrEmpty(packageInfo.LicenseText))
                    {
                        packageInfo.LicenseText = await _licenseCacheService.DownloadContentAsync(metadata.LicenseUrl);
                    }
                }
            }
            // Handle legacy license URL (deprecated but still used by some packages)
            else if (metadata.LicenseUrl != null)
            {
                packageInfo.LicenseType = "External";
                packageInfo.LicenseUrl = metadata.LicenseUrl.ToString();
                packageInfo.LicenseText = await _licenseCacheService.DownloadContentAsync(metadata.LicenseUrl);
            }
            else
            {
                // No license information available
                packageInfo.LicenseType = "Not specified";
                packageInfo.LicenseText = "License not specified";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting license info for {packageInfo.Name}: {ex.Message}");
            packageInfo.LicenseType = "Error";
            packageInfo.LicenseText = $"Error retrieving license information: {ex.Message}";
        }
    }

    /// <summary>
    /// Retrieves standard license text for SPDX license expressions (supports compound expressions like "MIT AND Apache-2.0")
    /// </summary>
    /// <param name="licenseExpression">SPDX license identifier or expression (e.g., MIT, Apache-2.0, "MIT AND Apache-2.0")</param>
    /// <returns>License text content for the specified license(s)</returns>
    private async Task<string> GetLicenseTextFromExpressionAsync(string licenseExpression)
    {
        try
        {
            // Parse compound license expressions (AND, OR, WITH)
            var licenseIds = ParseLicenseExpression(licenseExpression);

            if (licenseIds.Count == 1)
            {
                // Single license
                return await GetStandardLicenseTextAsync(licenseIds[0]);
            }
            else
            {
                // Multiple licenses - combine texts
                var combinedText = new StringBuilder();

                for (int i = 0; i < licenseIds.Count; i++)
                {
                    var licenseId = licenseIds[i];
                    combinedText.AppendLine($"--- LICENSE {i + 1}: {licenseId} ---");
                    combinedText.AppendLine();

                    var licenseText = await GetStandardLicenseTextAsync(licenseId);
                    combinedText.AppendLine(licenseText);

                    if (i < licenseIds.Count - 1)
                    {
                        combinedText.AppendLine();
                        combinedText.AppendLine("=".PadRight(50, '='));
                        combinedText.AppendLine();
                    }
                }

                return combinedText.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting license text for {licenseExpression}: {ex.Message}");
            return $"Error retrieving license text for '{licenseExpression}'";
        }
    }

    /// <summary>
    /// Parses SPDX license expressions and extracts individual license identifiers
    /// </summary>
    /// <param name="licenseExpression">SPDX license expression</param>
    /// <returns>List of individual license identifiers</returns>
    private List<string> ParseLicenseExpression(string licenseExpression)
    {
        var licenseIds = new List<string>();

        if (string.IsNullOrWhiteSpace(licenseExpression))
        {
            return licenseIds;
        }

        // Handle compound expressions with AND, OR, WITH operators
        // Note: This is a simplified parser - for full SPDX parsing, consider using a dedicated library
        var operators = new[] { " AND ", " OR ", " WITH " };
        var expression = licenseExpression.Trim();

        // Split by operators and extract license IDs
        var parts = new List<string> { expression };

        foreach (var op in operators)
        {
            var newParts = new List<string>();
            foreach (var part in parts)
            {
                if (part.Contains(op))
                {
                    newParts.AddRange(part.Split(new[] { op }, StringSplitOptions.RemoveEmptyEntries));
                }
                else
                {
                    newParts.Add(part);
                }
            }
            parts = newParts;
        }

        // Clean up and validate license IDs
        foreach (var part in parts)
        {
            var cleanId = part.Trim().Trim('(', ')');
            if (!string.IsNullOrWhiteSpace(cleanId) && IsValidLicenseId(cleanId))
            {
                licenseIds.Add(cleanId);
            }
        }

        // If no valid licenses found, treat the whole expression as a single license ID
        if (licenseIds.Count == 0)
        {
            licenseIds.Add(licenseExpression.Trim());
        }

        return licenseIds;
    }

    /// <summary>
    /// Validates if a string appears to be a valid SPDX license identifier
    /// </summary>
    /// <param name="licenseId">License identifier to validate</param>
    /// <returns>True if the identifier appears valid</returns>
    private bool IsValidLicenseId(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId))
            return false;

        // Basic validation: license IDs typically contain letters, numbers, hyphens, and periods
        // and don't contain SPDX operators
        var invalidPatterns = new[] { " AND ", " OR ", " WITH ", "(", ")" };

        foreach (var pattern in invalidPatterns)
        {
            if (licenseId.Contains(pattern))
                return false;
        }

        // Must contain at least one letter or number
        return licenseId.Any(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Downloads standard license text from the SPDX license repository
    /// </summary>
    /// <param name="licenseId">SPDX license identifier</param>
    /// <returns>License text content or fallback message</returns>
    private async Task<string> GetStandardLicenseTextAsync(string licenseId)
    {
        // Check cache first
        var cachedText = await _licenseCacheService.GetCachedLicenseTextAsync(licenseId);
        if (cachedText != null)
        {
            return cachedText;
        }

        // Download from remote
        var licenseText = await _licenseCacheService.DownloadLicenseTextAsync(licenseId);

        // Cache the result
        await _licenseCacheService.CacheLicenseTextAsync(licenseId, licenseText);

        return licenseText;
    }
}