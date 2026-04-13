using System.Text.Json;

namespace MemoryToolkit.Maui.E2ETests;

public sealed record E2ETestReport(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string Platform,
    string PlatformVersion,
    string OutputPath,
    IReadOnlyList<E2ETestResult> Results)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public bool Passed => Results.All(result => result.Passed);

    public static string ResolveOutputPath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("MEMORYTOOLKIT_E2E_OUTPUT");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        return Path.Combine(FileSystem.Current.AppDataDirectory, "memorytoolkit-e2e-results.json");
    }

    public async Task WriteAsync(string outputPath)
    {
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        Directory.CreateDirectory(string.IsNullOrWhiteSpace(outputDirectory)
            ? FileSystem.Current.AppDataDirectory
            : outputDirectory);

        E2ETestReport report = this with { OutputPath = outputPath };
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }
}

public sealed record E2ETestResult(
    string Id,
    string Name,
    bool Passed,
    IReadOnlyDictionary<string, string> Observations,
    string? Failure = null);
