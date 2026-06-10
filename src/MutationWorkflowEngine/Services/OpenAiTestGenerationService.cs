using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        using var openAiHttp = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(config.ProcessTimeoutMinutes)
        };
        if (!string.IsNullOrWhiteSpace(config.OpenAiApiKey))
        {
            openAiHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.OpenAiApiKey);
        }

        using var geminiHttp = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(config.ProcessTimeoutMinutes)
        };

        var canUseOpenAi = !string.IsNullOrWhiteSpace(config.OpenAiApiKey) && !string.IsNullOrWhiteSpace(config.OpenAiModel);
        var canUseGemini = config.UseGeminiFallback &&
                           !string.IsNullOrWhiteSpace(config.GoogleApiKey) &&
                           !string.IsNullOrWhiteSpace(config.GeminiModel);

        if (!canUseOpenAi && !canUseGemini)
        {
            throw new InvalidOperationException("No AI generation provider available (OpenAI/Gemini). Check configuration.");
        }

        var semaphore = new SemaphoreSlim(config.MaxConcurrency);
        var tasks = generationPlan.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await GenerateSinglePatchAsync(openAiHttp, geminiHttp, config, framework, preCommitSummary, item, canUseOpenAi, canUseGemini, cancellationToken);
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
        HttpClient openAiHttp,
        HttpClient geminiHttp,
        AppConfig config,
        TestingFramework framework,
        MutationReportSummary preCommitSummary,
        (string SourceFileAbsolute, string RelativeSourceFile, string TestFileAbsolute, string RelativeTestFile) item,
        bool canUseOpenAi,
        bool canUseGemini,
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

        if (canUseOpenAi)
        {
            try
            {
                var openAiText = await GenerateWithOpenAiAsync(openAiHttp, config, prompt, cancellationToken);
                return ParsePatch(openAiText, item.RelativeTestFile);
            }
            catch (Exception ex) when (canUseGemini)
            {
                Console.WriteLine($"OpenAI generation failed for {item.RelativeSourceFile}. Falling back to Gemini. Reason: {ex.Message}");
            }
        }

        if (canUseGemini)
        {
            var geminiText = await GenerateWithGeminiAsync(geminiHttp, config, prompt, cancellationToken);
            return ParsePatch(geminiText, item.RelativeTestFile);
        }

        throw new InvalidOperationException($"Failed to generate tests for {item.RelativeSourceFile}.");
    }

    private static async Task<string> GenerateWithOpenAiAsync(
        HttpClient http,
        AppConfig config,
        string prompt,
        CancellationToken cancellationToken)
    {
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

        return ExtractResponseText(payload);
    }

    private static async Task<string> GenerateWithGeminiAsync(
        HttpClient http,
        AppConfig config,
        string prompt,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(config.GeminiModel)}:generateContent?key={Uri.EscapeDataString(config.GoogleApiKey)}";
        var payloadObj = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 4000,
                responseMimeType = "application/json"
            }
        };

        using var response = await http.PostAsync(
            endpoint,
            new StringContent(JsonSerializer.Serialize(payloadObj, JsonOptions), Encoding.UTF8, "application/json"),
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini request failed: {(int)response.StatusCode} {payload}");
        }

        return ExtractGeminiText(payload);
    }

    private static string ExtractGeminiText(string responsePayload)
    {
        using var doc = JsonDocument.Parse(responsePayload);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
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

        throw new InvalidOperationException("Gemini response did not contain usable text.");
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

        // Compress duplicate separators and remove accidental spaces around separators.
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
