namespace NuGetLicenseCollector.Interfaces;

/// <summary>
/// Service for managing license text caching operations
/// </summary>
public interface ILicenseCacheService
{
    /// <summary>
    /// Gets cached license text from memory or file cache
    /// </summary>
    /// <param name="licenseId">License identifier</param>
    /// <returns>Cached license text or null if not found</returns>
    Task<string?> GetCachedLicenseTextAsync(string licenseId);

    /// <summary>
    /// Stores license text in both memory and file cache
    /// </summary>
    /// <param name="licenseId">License identifier</param>
    /// <param name="licenseText">License text content</param>
    Task CacheLicenseTextAsync(string licenseId, string licenseText);

    /// <summary>
    /// Downloads license text from SPDX repository
    /// </summary>
    /// <param name="licenseId">License identifier</param>
    /// <returns>License text content</returns>
    Task<string> DownloadLicenseTextAsync(string licenseId);

    /// <summary>
    /// Downloads content from a specified URL
    /// </summary>
    /// <param name="url">URL to download from</param>
    /// <returns>Content as string</returns>
    Task<string> DownloadContentAsync(Uri url);

    /// <summary>
    /// Clears all cached license files
    /// </summary>
    void ClearCache();
}