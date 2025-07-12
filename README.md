# NuGet License Collector

A .NET tool to collect and analyze NuGet package licenses in a solution.

## Features

- **License Analysis**: Automatically detects and analyzes NuGet package licenses across all projects in a solution
- **Multiple Output Formats**: Supports both text and JSON output formats
- **Comprehensive Reports**: Generates detailed reports with package information and license details
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **Global Tool**: Install once and use anywhere via command line

## Installation

### Install as a global .NET tool

```bash
dotnet tool install -g EXCEEDSYSTEM.NuGetLicenseCollector
```

### Install from source

```bash
git clone https://github.com/exceedsystem/NuGetLicenseCollector.git
cd NuGetLicenseCollector
dotnet pack
dotnet tool install -g --add-source ./bin/Debug EXCEEDSYSTEM.NuGetLicenseCollector
```

## Uninstallation

### Uninstall the global tool

```bash
dotnet tool uninstall -g EXCEEDSYSTEM.NuGetLicenseCollector
```

### Clean up cache folder

After uninstalling, you may want to delete the cache folder to free up disk space:

**Windows:**
```cmd
rmdir /s "%USERPROFILE%\.exceedsystem\NuGetLicenseCollector"
```

**macOS/Linux:**
```bash
rm -rf ~/.exceedsystem/NuGetLicenseCollector
```

## Usage

### Basic Usage

```bash
nuget-license-collector path/to/your/solution.sln
```

### Analyze a single project

```bash
nuget-license-collector path/to/your/project.csproj
```

### Command Line Options

```bash
nuget-license-collector <input> [options]
```

#### Arguments

- `input` - Path to the solution file (.sln) or project file (.csproj, .vbproj)

#### Options

- `-o, --output <output>` - Output file path (default: "nuget-licenses.txt")
- `-j, --json` - Output in JSON format
- `-f, --force-refresh` - Clear license cache and download fresh license texts
- `--help` - Show help information
- `--version` - Show version information

### Examples

#### Generate a text report from solution

```bash
nuget-license-collector MySolution.sln
```

#### Generate a text report from project

```bash
nuget-license-collector MyProject.csproj
```

#### Generate a JSON report

```bash
nuget-license-collector MySolution.sln --json
```

#### Specify custom output file

```bash
nuget-license-collector MySolution.sln -o licenses-report.txt
```

#### Generate JSON report with custom filename

```bash
nuget-license-collector MySolution.sln -j -o my-licenses.json
```

#### Force refresh cached licenses

```bash
nuget-license-collector MySolution.sln --force-refresh
```

## Output Formats

### Text Format

The default text format provides a human-readable report with:
- Package name and version
- Author information
- License type and full license text
- Project URL and license URL (if available)
- License summary grouped by license type

### JSON Format

The JSON format provides structured data suitable for further processing:

```json
{
  "packages": [
    {
      "name": "PackageName",
      "version": "1.0.0",
      "author": "Author Name",
      "licenseType": "MIT",
      "licenseText": "License text content...",
      "licenseUrl": "https://...",
      "projectUrl": "https://..."
    }
  ],
  "summary": {
    "totalPackages": 10,
    "generatedAt": "2023-12-01T12:00:00Z"
  }
}
```

## Requirements

- .NET 8.0 or later
- Solution file (.sln) or project file (.csproj, .vbproj) with valid NuGet package references

## How it Works

1. **Input Analysis**: Parses the solution file (.sln) to discover all projects, or analyzes a single project file (.csproj, .vbproj)
2. **Package Discovery**: Analyzes project assets (`obj/project.assets.json`) to find NuGet package references
3. **License Retrieval**: Connects to NuGet.org to retrieve package metadata and license information with intelligent caching
4. **Report Generation**: Creates comprehensive reports in the specified format

## Features

- **Intelligent Caching**: Licenses are cached locally for 30 days to improve performance
- **Multiple Formats**: Supports RTF, HTML, and plain text license formats
- **Deduplication**: Automatically deduplicates packages across multiple projects
- **Network Resilience**: Implements retry logic for NuGet API calls
- **Cross-Platform**: Works on Windows, macOS, and Linux

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes to this project.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Troubleshooting

### Common Issues

1. **"File not found"** - Ensure the path to the solution or project file is correct
2. **"Unsupported file type"** - Only .sln, .csproj, and .vbproj files are supported
3. **"No packages found"** - Verify that your projects have NuGet package references and have been restored with `dotnet restore`
4. **Network errors** - Check your internet connection as the tool needs to access NuGet.org
5. **MSBuild errors** - Ensure the project is in a valid state and can be built

### Support

If you encounter any issues or have questions, please open an issue on the [GitHub repository](https://github.com/exceedsystem/NuGetLicenseCollector/issues).