namespace NuGetLicenseCollector.Interfaces;

public interface ISolutionAnalyzer
{
    /// <summary>
    /// Extracts all project file paths from a solution file
    /// </summary>
    /// <param name="solutionPath">Path to the solution (.sln) file</param>
    /// <returns>List of project file paths that exist on disk</returns>
    Task<List<string>> GetProjectFilesAsync(string solutionPath);

    /// <summary>
    /// Extracts NuGet package references from a project's lock file
    /// </summary>
    /// <param name="projectPath">Path to the project file (.csproj, .vbproj, etc.)</param>
    /// <returns>List of package names referenced by the project</returns>
    Task<List<string>> GetPackageReferencesAsync(string projectPath);
}