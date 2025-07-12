# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-07-12

### Added
- Initial release of NuGet License Collector
- Analyze NuGet package licenses across .NET solutions and projects
- Support for both solution (.sln) and project (.csproj, .vbproj) files
- Multiple output formats: text and JSON
- Intelligent license caching with 30-day expiration
- Cross-platform support (Windows, macOS, Linux)
- Global .NET tool packaging
- Command-line interface with comprehensive options
- License text extraction from various formats (RTF, HTML, plain text)
- Package deduplication across multiple projects
- Network resilience with retry logic for NuGet API calls
- License summary grouped by license type
- Force refresh option to clear cache and download fresh license texts