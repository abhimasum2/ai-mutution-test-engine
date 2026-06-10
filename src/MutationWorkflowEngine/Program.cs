using MutationWorkflowEngine.Models;
using MutationWorkflowEngine.Services;
using System.Text.Json;

namespace MutationWorkflowEngine;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var config = BuildConfig(args);
            var orchestrator = new WorkflowOrchestrator(
                new ProjectDiscoveryService(),
                new GitService(),
                new StrykerService(),
                new OpenAiTestGenerationService(),
                new TestIntegrationService(),
                new ReportService(),
                new ProcessRunner());

            await orchestrator.RunAsync(config, CancellationToken.None);
            Console.WriteLine("Workflow completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Workflow failed: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static AppConfig BuildConfig(string[] args)
    {
        var parsed = ParseArgs(args);
        var currentDirectory = Directory.GetCurrentDirectory();
        var configPath = GetValue(
            parsed,
            "input",
            GetValue(parsed, "config", Path.Combine(currentDirectory, "input.json")));
        var configFile = LoadConfigFile(configPath);
        var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? currentDirectory;

        var repositoryRootRaw = GetValue(parsed, "repo", configFile.RepositoryRoot ?? currentDirectory);
        var repositoryRoot = ResolvePath(repositoryRootRaw, configDirectory);

        var targetProjectFallback = configFile.TargetProjectPath ?? DiscoverTargetProjectPath(repositoryRoot);
        var targetProjectRaw = GetValue(parsed, "target", targetProjectFallback);

        var targetProjectPath = ResolvePath(targetProjectRaw, repositoryRoot);

        var testProjectRaw = GetValue(parsed, "test", configFile.TestProjectPath ?? string.Empty);
        var testProjectPath = string.IsNullOrWhiteSpace(testProjectRaw) ? null : ResolvePath(testProjectRaw, repositoryRoot);

        var reportsFallback = configFile.ReportsDirectory ?? "mutation-reports/default-run";
        var reportsRaw = GetValue(parsed, "reports", reportsFallback);

        var reportsDir = ResolvePath(reportsRaw, repositoryRoot);

        var baseRef = GetValue(parsed, "base", configFile.BaseRef ?? "origin/main");

        var model = GetValue(parsed, "openai-model", configFile.OpenAiModel ?? "gpt-4.1-mini");
        var apiKey = GetValue(parsed, "openai-key", configFile.OpenAiApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty);

        var useGeminiFallback = GetBool(parsed, "gemini-fallback", configFile.UseGeminiFallback, true);
        var googleApiKey = GetValue(parsed, "google-key", configFile.GoogleApiKey ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? string.Empty);
        var geminiModel = GetValue(parsed, "gemini-model", configFile.GeminiModel ?? "gemini-3.5-flash");

        var hasOpenAi = !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(model);
        var hasGemini = useGeminiFallback &&
                        !string.IsNullOrWhiteSpace(googleApiKey) &&
                        !string.IsNullOrWhiteSpace(geminiModel);

        if (!hasOpenAi && !hasGemini)
        {
            throw new InvalidOperationException("No AI provider configured. Provide OpenAiApiKey/OpenAiModel or enable Gemini fallback with GoogleApiKey/GeminiModel.");
        }

        var commitAndPush = GetBool(parsed, "commit", configFile.CommitAndPush, true);
        var verbose = GetBool(parsed, "verbose", configFile.Verbose, true);
        var generationMaxIterations = GetInt(parsed, "generation-max-iterations", configFile.GenerationMaxIterations, 3);
        var maxChars = GetInt(parsed, "max-source-chars", configFile.MaxSourceFileChars, 24000);
        var maxConcurrency = GetInt(parsed, "max-concurrency", configFile.MaxConcurrency, 4);
        var timeoutMinutes = GetInt(parsed, "process-timeout-minutes", configFile.ProcessTimeoutMinutes, 30);

        return new AppConfig(
            Path.GetFullPath(repositoryRoot),
            Path.GetFullPath(targetProjectPath),
            string.IsNullOrWhiteSpace(testProjectPath) ? null : Path.GetFullPath(testProjectPath),
            Path.GetFullPath(reportsDir),
            baseRef,
            model,
            apiKey,
            useGeminiFallback,
            googleApiKey,
            geminiModel,
            commitAndPush,
            Math.Max(1, generationMaxIterations),
            maxChars,
            Math.Max(1, maxConcurrency),
            Math.Max(5, timeoutMinutes),
            verbose);
    }

    private static AppConfigFile LoadConfigFile(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Input file not found: {fullPath}");
        }

        var json = File.ReadAllText(fullPath);
        var config = JsonSerializer.Deserialize<AppConfigFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException($"Unable to parse input file: {fullPath}");
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            dict[key] = value;
        }

        return dict;
    }

    private static string GetValue(Dictionary<string, string> args, string key, string fallback)
        => args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string? GetOptional(Dictionary<string, string> args, string key)
        => args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string ResolvePath(string path, string baseDirectory)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static bool GetBool(Dictionary<string, string> args, string key, bool? configValue, bool fallback)
    {
        if (args.TryGetValue(key, out var argValue) && bool.TryParse(argValue, out var parsedArg))
        {
            return parsedArg;
        }

        return configValue ?? fallback;
    }

    private static string DiscoverTargetProjectPath(string repositoryRoot)
    {
        var projectFiles = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Test", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (projectFiles.Count == 1)
        {
            return Path.GetRelativePath(repositoryRoot, projectFiles[0]);
        }

        var srcCandidates = projectFiles
            .Where(path => path.Contains("\\src\\", StringComparison.OrdinalIgnoreCase) || path.Contains("/src/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (srcCandidates.Count == 1)
        {
            return Path.GetRelativePath(repositoryRoot, srcCandidates[0]);
        }

        throw new InvalidOperationException(
            "Unable to auto-discover target project. Set TargetProjectPath in input.json or pass --target.");
    }

    private static int GetInt(Dictionary<string, string> args, string key, int? configValue, int fallback)
    {
        if (args.TryGetValue(key, out var argValue) && int.TryParse(argValue, out var parsedArg))
        {
            return parsedArg;
        }

        return configValue ?? fallback;
    }
}
