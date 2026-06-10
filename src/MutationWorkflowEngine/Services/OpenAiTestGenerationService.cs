using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class OpenAiTestGenerationService
{
    public async Task<(IReadOnlyList<GeneratedTestPatch> Patches, TokenUsageReport TokenUsage)> GenerateTestsAsync(
        AppConfig config,
        TestingFramework framework,
        MutationReportSummary preCommitSummary,
        IReadOnlyList<(string SourceFileAbsolute, string RelativeSourceFile, string TestFileAbsolute, string RelativeTestFile)> generationPlan,
        CancellationToken cancellationToken)
    {
        var canUseOpenAi = !string.IsNullOrWhiteSpace(config.OpenAiApiKey) && !string.IsNullOrWhiteSpace(config.OpenAiModel);
        if (!canUseOpenAi)
        {
            throw new InvalidOperationException("No OpenAI provider configured. Check OpenAiApiKey and OpenAiModel.");
        }

        var openAiClient = CreateChatClient(config);

        var semaphore = new SemaphoreSlim(config.MaxConcurrency);
        var tasks = generationPlan.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await GenerateSinglePatchAsync(openAiClient, config, framework, preCommitSummary, item, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        var patches = results.Where(r => r.Patch is not null).Select(r => r.Patch!).ToList();
        var usageRecords = results.Select(r => new TokenUsageRecord(
            r.SourceFile,
            r.InputTokens,
            r.OutputTokens,
            r.InputTokens + r.OutputTokens)).ToList();

        var tokenUsage = new TokenUsageReport(
            usageRecords.Sum(u => u.InputTokens),
            usageRecords.Sum(u => u.OutputTokens),
            usageRecords.Sum(u => u.TotalTokens),
            usageRecords);

        return (patches, tokenUsage);
    }

    private static async Task<(GeneratedTestPatch? Patch, string SourceFile, int InputTokens, int OutputTokens)> GenerateSinglePatchAsync(
        IChatClient openAiClient,
        AppConfig config,
        TestingFramework framework,
        MutationReportSummary preCommitSummary,
        (string SourceFileAbsolute, string RelativeSourceFile, string TestFileAbsolute, string RelativeTestFile) item,
        CancellationToken cancellationToken)
    {
        var sourceContent = await ReadTrimmedAsync(item.SourceFileAbsolute, config.MaxSourceFileChars, cancellationToken);
        var existingTestContent = File.Exists(item.TestFileAbsolute)
            ? await ReadTrimmedAsync(item.TestFileAbsolute, config.MaxSourceFileChars, cancellationToken)
            : string.Empty;

        var fileReport = preCommitSummary.Files.FirstOrDefault(f =>
            f.SourceFile.EndsWith(item.RelativeSourceFile.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

        var mutationHints = fileReport is null
            ? "No file-level mutation detail available."
            : $"Mutants={fileReport.Total}, Survived={fileReport.Survived}, Killed={fileReport.Killed}, Score={fileReport.Score:F2}";

        var prompt = BuildPrompt(framework, item.RelativeSourceFile, item.RelativeTestFile, mutationHints, sourceContent, existingTestContent);

        var (openAiText, inputTokens, outputTokens) = await GenerateWithOpenAiAsync(openAiClient, prompt, cancellationToken);
        return (ParsePatch(openAiText, item.RelativeTestFile), item.RelativeSourceFile, inputTokens, outputTokens);
    }

    private static async Task<(string Text, int InputTokens, int OutputTokens)> GenerateWithOpenAiAsync(
        IChatClient chatClient,
        string prompt,
        CancellationToken cancellationToken)
    {
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException("OpenAI response did not contain usable text.");
        }

        var inputTokens = (int)(response.Usage?.InputTokenCount ?? 0);
        var outputTokens = (int)(response.Usage?.OutputTokenCount ?? 0);
        return (response.Text, inputTokens, outputTokens);
    }

    private static IChatClient CreateChatClient(AppConfig config)
    {
        var baseUrl = NormalizeBaseUrl(config.OpenAiBaseUrl);
        var isDefaultEndpoint = string.Equals(baseUrl, "https://api.openai.com/v1/", StringComparison.OrdinalIgnoreCase);

        var client = isDefaultEndpoint
            ? new OpenAIClient(config.OpenAiApiKey)
            : new OpenAIClient(
                new ApiKeyCredential(config.OpenAiApiKey),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(baseUrl)
                });

        return client.GetChatClient(config.OpenAiModel).AsIChatClient();
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1/"
            : baseUrl.Trim();

        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    private static string BuildPrompt(
        TestingFramework framework,
        string sourceFile,
        string targetTestFile,
        string mutationHints,
        string sourceCode,
        string existingTests)
    {
        var frameworkRule = framework switch
        {
            TestingFramework.XUnit => "Use xUnit attributes [Fact]/[Theory].",
            TestingFramework.NUnit => "Use NUnit attributes [Test]/[TestCase].",
            TestingFramework.MSTest => "Use MSTest attributes [TestMethod]/[DataTestMethod].",
            _ => "Use the existing framework conventions from test file."
        };

        return $@"You are generating robust .NET unit tests to kill survived mutation cases.
Return only JSON with this exact schema:
{{
    ""relativeTestFilePath"": ""{targetTestFile}"",
    ""reasoning"": ""short reason"",
    ""content"": ""full compilable C# test file content""
}}

Rules:
- {frameworkRule}
- Keep namespace/project conventions compatible with existing tests.
- Include focused assertions and edge cases.
- Output complete file content, not a diff.
- No markdown fences.

Source file: {sourceFile}
Mutation hints: {mutationHints}

Source code:
{sourceCode}

Existing tests (may be empty):
{existingTests}";
    }

    private static GeneratedTestPatch ParsePatch(string rawText, string fallbackRelativePath)
    {
        var cleaned = NormalizeJsonPayload(rawText);

        using var doc = JsonDocument.Parse(cleaned);
        var root = doc.RootElement;
        var path = root.TryGetProperty("relativeTestFilePath", out var pathProp)
            ? pathProp.GetString() ?? fallbackRelativePath
            : fallbackRelativePath;
        path = SanitizeRelativeTestPath(path, fallbackRelativePath);

        var content = root.TryGetProperty("content", out var contentProp)
            ? contentProp.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Generated test patch contained empty content.");
        }

        var reasoning = root.TryGetProperty("reasoning", out var reasonProp)
            ? reasonProp.GetString() ?? string.Empty
            : string.Empty;

        return new GeneratedTestPatch(path, content, reasoning);
    }

    private static string SanitizeRelativeTestPath(string candidatePath, string fallbackRelativePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return fallbackRelativePath;
        }

        var normalized = candidatePath.Trim().Trim('"', '\'');
        normalized = normalized
            .Replace('\t', '/')
            .Replace('\r', '/')
            .Replace('\n', '/')
            .Replace('\\', '/');

        normalized = Regex.Replace(normalized, @"\s*/\s*", "/");
        normalized = Regex.Replace(normalized, @"/{2,}", "/");

        if (Path.IsPathRooted(normalized))
        {
            return fallbackRelativePath;
        }

        normalized = normalized.TrimStart('/');

        var invalidChars = Path.GetInvalidFileNameChars();
        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .Select(segment => new string(segment.Where(ch => !char.IsControl(ch) && !invalidChars.Contains(ch)).ToArray()).Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0)
        {
            return fallbackRelativePath;
        }

        var sanitized = Path.Combine(segments.ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? fallbackRelativePath : sanitized;
    }

    private static string NormalizeJsonPayload(string rawText)
    {
        var cleaned = rawText.Trim();

        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            cleaned = cleaned.Trim('`').Trim();
            if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[4..].Trim();
            }
        }

        if (LooksLikeJsonObject(cleaned))
        {
            return cleaned;
        }

        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var extracted = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            if (LooksLikeJsonObject(extracted))
            {
                return extracted;
            }
        }

        throw new InvalidOperationException("Model response was not valid JSON for test patch generation.");
    }

    private static bool LooksLikeJsonObject(string input)
    {
        try
        {
            using var _ = JsonDocument.Parse(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> ReadTrimmedAsync(string filePath, int maxChars, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        if (content.Length <= maxChars)
        {
            return content;
        }

        return content[..maxChars] + "\n// [truncated for prompt size]";
    }
}
