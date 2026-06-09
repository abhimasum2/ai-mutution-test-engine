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
                new ReportService());

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
        var configPath = GetValue(parsed, "config", Path.Combine(currentDirectory, "mutationworkflow.config.json"));
        var configFile = LoadConfigFile(configPath);
        var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? currentDirectory;

        var repositoryRootRaw = GetValue(parsed, "repo", configFile.RepositoryRoot ?? currentDirectory);
        var repositoryRoot = ResolvePath(repositoryRootRaw, configDirectory);

        var targetProjectRaw = GetValue(parsed, "target", configFile.TargetProjectPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(targetProjectRaw))
        {
            throw new InvalidOperationException("Missing target project path in config. Set TargetProjectPath in mutationworkflow.config.json or pass --target.");
        }

        var targetProjectPath = ResolvePath(targetProjectRaw, configDirectory);

        var testProjectRaw = GetValue(parsed, "test", configFile.TestProjectPath ?? string.Empty);
        var testProjectPath = string.IsNullOrWhiteSpace(testProjectRaw) ? null : ResolvePath(testProjectRaw, configDirectory);

        var reportsRaw = GetValue(parsed, "reports", configFile.ReportsDirectory ?? string.Empty);
        if (string.IsNullOrWhiteSpace(reportsRaw))
        {
            throw new InvalidOperationException("Missing reports directory in config. Set ReportsDirectory in mutationworkflow.config.json or pass --reports.");
        }

        var reportsDir = ResolvePath(reportsRaw, configDirectory);

        var baseRef = GetValue(parsed, "base", configFile.BaseRef ?? string.Empty);
        if (string.IsNullOrWhiteSpace(baseRef))
        {
            throw new InvalidOperationException("Missing base ref in config. Set BaseRef in mutationworkflow.config.json or pass --base.");
        }

        var model = GetValue(parsed, "openai-model", configFile.OpenAiModel ?? string.Empty);
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Missing OpenAI model in config. Set OpenAiModel in mutationworkflow.config.json or pass --openai-model.");
        }

        var apiKey = GetValue(parsed, "openai-key", configFile.OpenAiApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Missing OpenAI key. Set OpenAiApiKey in config, pass --openai-key, or set OPENAI_API_KEY.");
        }

        var commitAndPush = GetBool(parsed, "commit", configFile.CommitAndPush, true);
        var verbose = GetBool(parsed, "verbose", configFile.Verbose, true);
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
            commitAndPush,
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
            throw new FileNotFoundException($"Config file not found: {fullPath}");
        }

        var json = File.ReadAllText(fullPath);
        var config = JsonSerializer.Deserialize<AppConfigFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException($"Unable to parse config file: {fullPath}");
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

    private static int GetInt(Dictionary<string, string> args, string key, int? configValue, int fallback)
    {
        if (args.TryGetValue(key, out var argValue) && int.TryParse(argValue, out var parsedArg))
        {
            return parsedArg;
        }

        return configValue ?? fallback;
    }
}
