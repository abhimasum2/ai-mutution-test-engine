using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class OpenAiTestGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<IReadOnlyList<GeneratedTestPatch>> GenerateTestsAsync(
        AppConfig config,
        TestingFramework framework,
        MutationReportSummary preCommitSummary,
        IReadOnlyList<(string SourceFileAbsolute, string RelativeSourceFile, string TestFileAbsolute, string RelativeTestFile)> generationPlan,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(config.ProcessTimeoutMinutes)
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.OpenAiApiKey);

        var semaphore = new SemaphoreSlim(config.MaxConcurrency);
        var tasks = generationPlan.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await GenerateSinglePatchAsync(http, config, framework, preCommitSummary, item, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var generated = await Task.WhenAll(tasks);
        return generated.Where(p => p is not null).Cast<GeneratedTestPatch>().ToList();
    }

    private async Task<GeneratedTestPatch?> GenerateSinglePatchAsync(
        HttpClient http,
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
        var requestPayload = new
        {
            model = config.OpenAiModel,
            input = prompt,
            max_output_tokens = 4000
        };

        using var response = await http.PostAsync(
            "https://api.openai.com/v1/responses",
            new StringContent(JsonSerializer.Serialize(requestPayload, JsonOptions), Encoding.UTF8, "application/json"),
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI request failed: {(int)response.StatusCode} {payload}");
        }

        var modelText = ExtractResponseText(payload);
        var patch = ParsePatch(modelText, item.RelativeTestFile);
        return patch;
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
        var cleaned = rawText.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            cleaned = cleaned.Trim('`').Trim();
            if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[4..].Trim();
            }
        }

        using var doc = JsonDocument.Parse(cleaned);
        var root = doc.RootElement;
        var path = root.TryGetProperty("relativeTestFilePath", out var pathProp)
            ? pathProp.GetString() ?? fallbackRelativePath
            : fallbackRelativePath;

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

    private static string ExtractResponseText(string responsePayload)
    {
        using var doc = JsonDocument.Parse(responsePayload);
        var root = doc.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var outputArray) && outputArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in outputArray.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException("Unable to extract textual response from OpenAI payload.");
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
