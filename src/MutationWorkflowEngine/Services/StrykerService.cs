using System.Text.Json;
using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class StrykerService
{
    private readonly ProcessRunner _runner = new();

    public async Task<string> RunMutationAsync(
        string repositoryRoot,
        string targetProjectPath,
        string testProjectPath,
        IReadOnlyList<string> changedFiles,
        string reportOutputDirectory,
        string runName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (changedFiles.Count == 0)
        {
            throw new InvalidOperationException("No changed files to mutate.");
        }

        Directory.CreateDirectory(reportOutputDirectory);
        var strykerConfigPath = Path.Combine(reportOutputDirectory, $"stryker-config-{runName}.json");
        var mutatePatterns = changedFiles.Select(ToGlobPattern).Distinct().ToList();

        var config = new
        {
            mutate = mutatePatterns,
            reporters = new[] { "json", "html", "cleartext" },
            testRunner = "dotnettest",
            project = Path.GetFileName(targetProjectPath),
            testProjects = new[] { testProjectPath }
        };

        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(strykerConfigPath, configJson, cancellationToken);

        var result = await _runner.RunAsync(
            "dotnet",
            $"stryker --config-file \"{strykerConfigPath}\" --output \"{reportOutputDirectory}\"",
            repositoryRoot,
            timeout,
            cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Stryker run failed ({runName}). {result.StdErr}\n{result.StdOut}");
        }

        var reportPath = FindMutationJsonReport(reportOutputDirectory);
        if (reportPath is null)
        {
            throw new FileNotFoundException($"Mutation report not found in {reportOutputDirectory}");
        }

        return reportPath;
    }

    public async Task<MutationReportSummary> ParseReportAsync(string reportPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(reportPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = doc.RootElement;
        var files = new List<MutationFileResult>();
        var totalKilled = 0;
        var totalSurvived = 0;
        var totalMutants = 0;

        if (root.TryGetProperty("files", out var filesObject) && filesObject.ValueKind == JsonValueKind.Object)
        {
            foreach (var fileProp in filesObject.EnumerateObject())
            {
                var filePath = fileProp.Name;
                var killed = 0;
                var survived = 0;
                var total = 0;

                if (!fileProp.Value.TryGetProperty("mutants", out var mutants) || mutants.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var mutant in mutants.EnumerateArray())
                {
                    total++;
                    if (!mutant.TryGetProperty("status", out var statusProp))
                    {
                        continue;
                    }

                    var status = statusProp.GetString() ?? string.Empty;
                    if (status.Equals("Killed", StringComparison.OrdinalIgnoreCase))
                    {
                        killed++;
                    }
                    else if (status.Equals("Survived", StringComparison.OrdinalIgnoreCase))
                    {
                        survived++;
                    }
                }

                var score = total == 0 ? 0 : (double)killed / total * 100;
                files.Add(new MutationFileResult(filePath, killed, survived, total, score));
                totalKilled += killed;
                totalSurvived += survived;
                totalMutants += total;
            }
        }

        var overallScore = totalMutants == 0 ? 0 : (double)totalKilled / totalMutants * 100;
        return new MutationReportSummary(reportPath, totalKilled, totalSurvived, totalMutants, overallScore, files);
    }

    private static string ToGlobPattern(string relativeFile)
        => "**/" + relativeFile.Replace('\\', '/');

    private static string? FindMutationJsonReport(string outputDirectory)
    {
        var candidates = Directory.EnumerateFiles(outputDirectory, "mutation-report.json", SearchOption.AllDirectories).ToList();
        if (candidates.Count > 0)
        {
            return candidates.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }

        return null;
    }
}
