using System.CommandLine;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using NuGetLicenseCollector.Interfaces;
using NuGetLicenseCollector.Services;

namespace NuGetLicenseCollector;

internal class Program
{
    private const string SolutionExtension = ".sln";
    private const string CSharpProjectExtension = ".csproj";
    private const string VBProjectExtension = ".vbproj";
    private const string JsonExtension = ".json";
    private const string TextExtension = ".txt";

    private static async Task Main(string[] args)
    {
        var inputFile = new Argument<string>(
            name: "input",
            description: $"Path to the solution file ({SolutionExtension}) or project file ({CSharpProjectExtension}, {VBProjectExtension})");

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output file path",
            getDefaultValue: () => "nuget-licenses.txt");
        outputOption.AddAlias("-o");

        var jsonOption = new Option<bool>(
            name: "--json",
            description: "Output in JSON format");
        jsonOption.AddAlias("-j");

        var forceRefreshOption = new Option<bool>(
            name: "--force-refresh",
            description: "Clear license cache and download fresh license texts");
        forceRefreshOption.AddAlias("-f");

        var rootCommand = new RootCommand("NuGet License Collector - Analyze NuGet package licenses in a solution or project")
        {
            inputFile,
            outputOption,
            jsonOption,
            forceRefreshOption
        };

        rootCommand.SetHandler(async (inputPath, outputPath, useJson, forceRefresh) =>
        {
            if (useJson && !outputPath.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.ChangeExtension(outputPath, JsonExtension);
            }

            await ProcessFileAsync(inputPath, outputPath, useJson, forceRefresh);
        }, inputFile, outputOption, jsonOption, forceRefreshOption);

        await rootCommand.InvokeAsync(args);
    }

    private static ServiceProvider ConfigureServices(bool forceRefresh)
    {
        var services = new ServiceCollection();

        services.AddHttpClient();
        services.AddSingleton<ISolutionAnalyzer, SolutionAnalyzer>();
        services.AddSingleton<ILicenseCacheService>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return new LicenseCacheService(httpClient);
        });
        services.AddSingleton<IReportGenerator, ReportGenerator>();
        services.AddSingleton<INuGetLicenseAnalyzerService>(provider =>
            new NuGetLicenseAnalyzerService(provider.GetRequiredService<ILicenseCacheService>(), forceRefresh));

        return services.BuildServiceProvider();
    }

    private static async Task ProcessFileAsync(string filePath, string outputPath, bool useJson, bool forceRefresh)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File '{filePath}' not found.");
            return;
        }

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        if (fileExtension != SolutionExtension && fileExtension != CSharpProjectExtension && fileExtension != VBProjectExtension)
        {
            Console.WriteLine($"Error: Unsupported file type '{fileExtension}'. Supported types: {SolutionExtension}, {CSharpProjectExtension}, {VBProjectExtension}");
            return;
        }

        try
        {
            // CRITICAL: MSBuildLocator must be registered before using Microsoft.Build APIs
            // This enables the tool to locate and use the correct MSBuild assemblies
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            Console.WriteLine($"Analyzing file: {filePath}");
            Console.WriteLine("This may take a while...");
            using var serviceProvider = ConfigureServices(forceRefresh);
            var solutionAnalyzer = serviceProvider.GetRequiredService<ISolutionAnalyzer>();
            var licenseAnalyzerService = serviceProvider.GetRequiredService<INuGetLicenseAnalyzerService>();
            var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();

            var projectFiles = new List<string>();

            if (fileExtension == SolutionExtension)
            {
                projectFiles = await solutionAnalyzer.GetProjectFilesAsync(filePath);
                Console.WriteLine($"Found {projectFiles.Count} projects");
            }
            else if (fileExtension == CSharpProjectExtension || fileExtension == VBProjectExtension)
            {
                projectFiles.Add(filePath);
                Console.WriteLine($"Analyzing project file: {Path.GetFileName(filePath)}");
            }
            else
            {
                Console.WriteLine($"Unsupported file type: {fileExtension}");
                return;
            }

            // Use HashSet to automatically deduplicate packages across multiple projects
            var allPackages = new HashSet<string>();

            foreach (var projectFile in projectFiles)
            {
                Console.WriteLine($"Analyzing project: {Path.GetFileName(projectFile)}");
                var packages = await solutionAnalyzer.GetPackageReferencesAsync(projectFile);

                foreach (var package in packages)
                {
                    allPackages.Add(package);
                }
            }

            Console.WriteLine($"Found {allPackages.Count} unique packages");

            var packageInfos = await licenseAnalyzerService.GetPackageInfoAsync(allPackages.ToList());
            Console.WriteLine($"Retrieved license information for {packageInfos.Count} packages");

            if (useJson)
            {
                await reportGenerator.GenerateJsonReportAsync(packageInfos, outputPath);
            }
            else
            {
                await reportGenerator.GenerateReportAsync(packageInfos, outputPath);
            }

            Console.WriteLine("Analysis complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}