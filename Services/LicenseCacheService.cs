using System.Text.RegularExpressions;
using HtmlAgilityPack;
using NuGetLicenseCollector.Interfaces;
using RtfPipe;

namespace NuGetLicenseCollector.Services;

/// <summary>
/// Service for managing license text caching and downloading operations
/// </summary>
public class LicenseCacheService : ILicenseCacheService, IDisposable
{
    // Cache expiration: 30 days balances freshness with performance
    // License texts rarely change, but we want to catch updates eventually
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(30);

    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _licenseTextCache;
    private readonly string _licenseCacheDirectory;

    public LicenseCacheService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _licenseTextCache = new Dictionary<string, string>();

        // Store cache in user profile to avoid permission issues and per-user isolation
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _licenseCacheDirectory = Path.Combine(userProfilePath, ".exceedsystem", "NuGetLicenseCollector", "cache", "licenses");
        Directory.CreateDirectory(_licenseCacheDirectory);
    }

    /// <inheritdoc />
    public async Task<string?> GetCachedLicenseTextAsync(string licenseId)
    {
        // Check memory cache first
        if (_licenseTextCache.TryGetValue(licenseId, out var cachedText))
        {
            return cachedText;
        }

        // Check local file cache
        var localCachePath = Path.Combine(_licenseCacheDirectory, $"{licenseId}.txt");
        if (File.Exists(localCachePath))
        {
            var fileInfo = new FileInfo(localCachePath);
            if (DateTime.Now - fileInfo.CreationTime < CacheExpiration)
            {
                try
                {
                    var localContent = await File.ReadAllTextAsync(localCachePath);
                    _licenseTextCache[licenseId] = localContent;
                    return localContent;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading local cache for {licenseId}: {ex.Message}");
                }
            }
            else
            {
                // Cache expired, delete old file
                try
                {
                    File.Delete(localCachePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting expired cache for {licenseId}: {ex.Message}");
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task CacheLicenseTextAsync(string licenseId, string licenseText)
    {
        // Cache in memory
        _licenseTextCache[licenseId] = licenseText;

        // Cache locally (only if not fallback text)
        // Only cache successful downloads, not error messages (indicated by "!!!")
        if (!licenseText.StartsWith("!!!"))
        {
            try
            {
                var localCachePath = Path.Combine(_licenseCacheDirectory, $"{licenseId}.txt");
                await File.WriteAllTextAsync(localCachePath, licenseText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving local cache for {licenseId}: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> DownloadLicenseTextAsync(string licenseId)
    {
        try
        {
            // Use official SPDX license repository for standard license texts
            // This ensures we get canonical, legally accurate license text
            var url = $"https://raw.githubusercontent.com/spdx/license-list-data/master/text/{licenseId}.txt";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                if (!IsLikelyHtml(content))
                {
                    return content;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading license {licenseId}: {ex.Message}");
        }

        return GetFallbackLicenseText(licenseId);
    }

    /// <inheritdoc />
    public async Task<string> DownloadContentAsync(Uri url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                // Skip HTML content as it's not suitable for text-based license reports
                // This prevents displaying raw HTML markup in output files
                if (IsLikelyHtml(content))
                {
                    return string.Empty;
                }

                // Convert RTF format to plain text
                if (IsLikelyRtf(content))
                {
                    return ConvertRtfToPlainText(content);
                }

                return content;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading content from {url}: {ex.Message}");
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_licenseCacheDirectory))
            {
                var cacheFiles = Directory.GetFiles(_licenseCacheDirectory, "*.txt");
                foreach (var file in cacheFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not delete cache file {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
                Console.WriteLine($"Cleared {cacheFiles.Length} license cache files");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error clearing license cache: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // HttpClient is managed by IHttpClientFactory - disposal is handled automatically
        // This follows the recommended pattern for HttpClient usage in .NET applications
    }

    /// <summary>
    /// Determines if content is likely HTML based on basic markup detection
    /// </summary>
    private bool IsLikelyHtml(string content)
    {
        return content.TrimStart().StartsWith("<") && content.Contains("</");
    }

    /// <summary>
    /// Determines if content is likely RTF based on RTF format markers
    /// </summary>
    private bool IsLikelyRtf(string content)
    {
        return content.TrimStart().StartsWith(@"{\rtf") || content.TrimStart().StartsWith(@"\rtf");
    }

    /// <summary>
    /// Converts RTF content to plain text using HTML intermediate format
    /// </summary>
    private string ConvertRtfToPlainText(string rtfContent)
    {
        try
        {
            var html = Rtf.ToHtml(rtfContent);
            var plainText = ExtractTextFromHtml(html);
            return plainText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting RTF to plain text: {ex.Message}");
            return rtfContent;
        }
    }

    /// <summary>
    /// Extracts plain text from HTML content while preserving basic formatting
    /// </summary>
    private string ExtractTextFromHtml(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find block-level elements that should create line breaks
            var blockElements = doc.DocumentNode.SelectNodes("//p | //div | //br | //h1 | //h2 | //h3 | //h4 | //h5 | //h6 | //li | //tr");
            if (blockElements != null)
            {
                foreach (var element in blockElements)
                {
                    if (element.Name.Equals("br", StringComparison.OrdinalIgnoreCase))
                    {
                        // Replace <br> with newline
                        element.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), element);
                    }
                    else
                    {
                        // Add newlines before and after block elements
                        var newlineNode = HtmlNode.CreateNode("\n");
                        element.ParentNode.InsertBefore(newlineNode.CloneNode(false), element);
                        element.ParentNode.InsertAfter(newlineNode.CloneNode(false), element);
                    }
                }
            }

            // Extract text content and clean up
            var plainText = doc.DocumentNode.InnerText;
            plainText = System.Net.WebUtility.HtmlDecode(plainText);
            // Normalize whitespace
            plainText = Regex.Replace(plainText, @"[ \t]+", " ");
            // Remove excessive newlines
            plainText = Regex.Replace(plainText, @"\n\s*\n\s*\n+", "\n\n");

            return plainText.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting text from HTML: {ex.Message}");
            // Fallback: simple regex-based HTML tag removal
            var text = Regex.Replace(html, @"<[^>]+>", "");
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }
    }

    /// <summary>
    /// Provides fallback message when license text cannot be retrieved
    /// </summary>
    private string GetFallbackLicenseText(string licenseId)
    {
        return $"!!! License text for '{licenseId}' could not be retrieved. !!!";
    }
}