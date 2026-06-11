using System.Net;
using System.Text;
using System.Text.Json;
using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class ReportService
{
    public async Task<(string JsonPath, string MarkdownPath)> WriteUnifiedReportAsync(
        string reportsDirectory,
        MutationReportSummary pre,
        MutationReportSummary post,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(reportsDirectory);
        var unified = BuildUnified(pre, post);

        var jsonPath = Path.Combine(reportsDirectory, "unified-mutation-report.json");
        var mdPath = Path.Combine(reportsDirectory, "unified-mutation-report.md");

        var json = JsonSerializer.Serialize(unified, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        await File.WriteAllTextAsync(mdPath, BuildMarkdown(unified), cancellationToken);

        return (jsonPath, mdPath);
    }

    public async Task<string> WriteTokenUsageReportAsync(
        string reportsDirectory,
        TokenUsageReport report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(reportsDirectory);

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            totalInputTokens = report.TotalInputTokens,
            totalOutputTokens = report.TotalOutputTokens,
            totalTokens = report.TotalTokens,
            perFile = report.Records.Select(r => new
            {
                sourceFile = r.SourceFile,
                inputTokens = r.InputTokens,
                outputTokens = r.OutputTokens,
                totalTokens = r.TotalTokens
            })
        };

        var jsonPath = Path.Combine(reportsDirectory, "token-usage-report.json");
        var mdPath = Path.Combine(reportsDirectory, "token-usage-report.md");

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("# Token Usage Report");
        sb.AppendLine();
        sb.AppendLine($"- Generated (UTC): {payload.generatedAtUtc:O}");
        sb.AppendLine($"- Total input tokens:  {report.TotalInputTokens}");
        sb.AppendLine($"- Total output tokens: {report.TotalOutputTokens}");
        sb.AppendLine($"- Total tokens:        {report.TotalTokens}");
        sb.AppendLine();
        sb.AppendLine("## Per-File Breakdown");
        sb.AppendLine();
        sb.AppendLine("| Source File | Input Tokens | Output Tokens | Total Tokens |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var r in report.Records)
        {
            sb.AppendLine($"| {r.SourceFile} | {r.InputTokens} | {r.OutputTokens} | {r.TotalTokens} |");
        }

        await File.WriteAllTextAsync(mdPath, sb.ToString(), cancellationToken);

        return jsonPath;
    }

    public async Task<string> WritePerformanceReportAsync(
        string reportsDirectory,
        PerformanceReport report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(reportsDirectory);

        var payload = new
        {
            startedAtUtc = report.StartedAtUtc,
            finishedAtUtc = report.FinishedAtUtc,
            totalDurationSeconds = report.TotalDuration.TotalSeconds,
            totalDurationFormatted = FormatDuration(report.TotalDuration),
            stages = report.Stages.Select(s => new
            {
                stage = s.Stage,
                durationSeconds = s.Duration.TotalSeconds,
                durationFormatted = FormatDuration(s.Duration)
            })
        };

        var jsonPath = Path.Combine(reportsDirectory, "performance-report.json");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        return jsonPath;
    }

    public async Task<string> WriteSummaryHtmlAsync(
        string reportsDirectory,
        MutationReportSummary pre,
        MutationReportSummary post,
        TokenUsageReport tokenUsage,
        PerformanceReport perf,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(reportsDirectory);

        var delta = post.Score - pre.Score;
        var deltaClass = delta > 0 ? "delta-pos" : (delta < 0 ? "delta-neg" : string.Empty);
        var deltaSign = delta >= 0 ? "+" : string.Empty;

        var preMap = pre.Files.ToDictionary(f => f.SourceFile, StringComparer.OrdinalIgnoreCase);
        var postMap = post.Files.ToDictionary(f => f.SourceFile, StringComparer.OrdinalIgnoreCase);
        var allFiles = preMap.Keys.Union(postMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x);

        var fileRows = new StringBuilder();
        foreach (var file in allFiles)
        {
            preMap.TryGetValue(file, out var pf);
            postMap.TryGetValue(file, out var po);
            var fd = (po?.Score ?? 0) - (pf?.Score ?? 0);
            var fdClass = fd > 0 ? "delta-pos" : (fd < 0 ? "delta-neg" : string.Empty);
            var fdSign = fd >= 0 ? "+" : string.Empty;
            fileRows.Append("<tr>")
                .Append($"<td>{H(file)}</td>")
                .Append($"<td>{pf?.Score ?? 0:F1}%</td>")
                .Append($"<td>{po?.Score ?? 0:F1}%</td>")
                .Append($"<td class=\"{fdClass}\">{fdSign}{fd:F1}%</td>")
                .Append($"<td>{pf?.Survived ?? 0}</td>")
                .AppendLine($"<td>{po?.Survived ?? 0}</td></tr>");
        }

        var tokenRows = new StringBuilder();
        foreach (var r in tokenUsage.Records)
            tokenRows.Append("<tr>")
                .Append($"<td>{H(r.SourceFile)}</td>")
                .Append($"<td>{r.InputTokens:N0}</td>")
                .Append($"<td>{r.OutputTokens:N0}</td>")
                .AppendLine($"<td>{r.TotalTokens:N0}</td></tr>");

        var stageRows = new StringBuilder();
        foreach (var s in perf.Stages)
            stageRows.Append("<tr>")
                .Append($"<td>{H(s.Stage)}</td>")
                .AppendLine($"<td>{FormatDuration(s.Duration)}</td></tr>");

        var html = new StringBuilder();
        html.Append(HtmlHead())
            .AppendLine("<h1>Mutation Engine Summary Report</h1>")
            .AppendLine($"<p class=\"meta\">Generated: {H(DateTime.UtcNow.ToString("O"))} UTC &nbsp;|&nbsp; Runtime: {H(FormatDuration(perf.TotalDuration))} &nbsp;|&nbsp; Started: {H(perf.StartedAtUtc.ToString("HH:mm:ss"))} UTC</p>")
            .AppendLine("<div class=\"grid\">")
            .AppendLine(
                $"<div class=\"card pre\"><h2>Pre-commit</h2>" +
                $"<div class=\"metric\">{pre.Score:F1}%</div><div class=\"sublabel\">Mutation Score</div>" +
                $"<div class=\"kv\"><span>Mutants</span><span class=\"val\">{pre.TotalMutants}</span></div>" +
                $"<div class=\"kv\"><span>Killed</span><span class=\"val\">{pre.TotalKilled}</span></div>" +
                $"<div class=\"kv\"><span>Survived</span><span class=\"val\">{pre.TotalSurvived}</span></div></div>")
            .AppendLine(
                $"<div class=\"card post\"><h2>Post-commit</h2>" +
                $"<div class=\"metric\">{post.Score:F1}%</div>" +
                $"<div class=\"sublabel\">Mutation Score &nbsp;<span class=\"{deltaClass}\">{deltaSign}{delta:F1}%</span></div>" +
                $"<div class=\"kv\"><span>Mutants</span><span class=\"val\">{post.TotalMutants}</span></div>" +
                $"<div class=\"kv\"><span>Killed</span><span class=\"val\">{post.TotalKilled}</span></div>" +
                $"<div class=\"kv\"><span>Survived</span><span class=\"val\">{post.TotalSurvived}</span></div></div>")
            .AppendLine(
                $"<div class=\"card tokens\"><h2>Token Usage</h2>" +
                $"<div class=\"metric\">{tokenUsage.TotalTokens:N0}</div><div class=\"sublabel\">Total Tokens</div>" +
                $"<div class=\"kv\"><span>Input</span><span class=\"val\">{tokenUsage.TotalInputTokens:N0}</span></div>" +
                $"<div class=\"kv\"><span>Output</span><span class=\"val\">{tokenUsage.TotalOutputTokens:N0}</span></div>" +
                $"<div class=\"kv\"><span>Files</span><span class=\"val\">{tokenUsage.Records.Count}</span></div></div>")
            .AppendLine(
                $"<div class=\"card perf\"><h2>Performance</h2>" +
                $"<div class=\"metric\">{H(FormatDuration(perf.TotalDuration))}</div><div class=\"sublabel\">Total Runtime</div>" +
                $"<div class=\"kv\"><span>Started (UTC)</span><span class=\"val\">{H(perf.StartedAtUtc.ToString("HH:mm:ss"))}</span></div>" +
                $"<div class=\"kv\"><span>Finished (UTC)</span><span class=\"val\">{H(perf.FinishedAtUtc.ToString("HH:mm:ss"))}</span></div>" +
                $"<div class=\"kv\"><span>Stages</span><span class=\"val\">{perf.Stages.Count}</span></div></div>")
            .AppendLine("</div>")
            .AppendLine("<div class=\"section\"><h2>File-Level Mutation Delta</h2>")
            .AppendLine("<table><tr><th>Source File</th><th>Pre Score</th><th>Post Score</th><th>Delta</th><th>Pre Survived</th><th>Post Survived</th></tr>")
            .Append(fileRows)
            .AppendLine("</table></div>")
            .AppendLine("<div class=\"section\"><h2>Token Usage per File</h2>")
            .AppendLine("<table><tr><th>Source File</th><th>Input Tokens</th><th>Output Tokens</th><th>Total Tokens</th></tr>")
            .Append(tokenRows)
            .AppendLine("</table></div>")
            .AppendLine("<div class=\"section\"><h2>Stage Timings</h2>")
            .AppendLine("<table><tr><th>Stage</th><th>Duration</th></tr>")
            .Append(stageRows)
            .AppendLine("</table></div>")
            .Append("</body></html>");

        var htmlPath = Path.Combine(reportsDirectory, "summary-report.html");
        await File.WriteAllTextAsync(htmlPath, html.ToString(), cancellationToken);
        return htmlPath;
    }

    private static string HtmlHead() =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        "<title>Mutation Engine Summary</title><style>" +
        "body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;margin:0;padding:24px;background:#f0f2f5;color:#333}" +
        "h1{color:#1a1a2e;margin-bottom:4px}" +
        ".meta{color:#888;font-size:.85em;margin-bottom:24px}" +
        ".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px;margin-bottom:28px}" +
        ".card{background:#fff;border-radius:10px;padding:20px;box-shadow:0 1px 4px rgba(0,0,0,.08)}" +
        ".card.pre{border-top:4px solid #3b82f6}.card.post{border-top:4px solid #22c55e}" +
        ".card.tokens{border-top:4px solid #a855f7}.card.perf{border-top:4px solid #f97316}" +
        ".card h2{margin:0 0 12px;font-size:.85em;color:#555;text-transform:uppercase;letter-spacing:.05em}" +
        ".metric{font-size:2.2em;font-weight:700;line-height:1;margin-bottom:4px}" +
        ".sublabel{color:#888;font-size:.82em;margin-bottom:12px}" +
        ".kv{display:flex;justify-content:space-between;font-size:.85em;padding:3px 0;border-bottom:1px solid #f0f0f0}" +
        ".kv:last-child{border-bottom:none}.kv .val{font-weight:600}" +
        ".delta-pos{color:#22c55e}.delta-neg{color:#ef4444}" +
        ".section{background:#fff;border-radius:10px;padding:20px;box-shadow:0 1px 4px rgba(0,0,0,.08);margin-bottom:20px}" +
        ".section h2{margin:0 0 14px;font-size:.85em;color:#555;text-transform:uppercase;letter-spacing:.05em}" +
        "table{width:100%;border-collapse:collapse;font-size:.85em}" +
        "th{background:#f8f9fa;padding:8px 12px;text-align:left;color:#666;font-weight:600;border-bottom:2px solid #e9ecef}" +
        "td{padding:7px 12px;border-bottom:1px solid #f0f0f0}" +
        "tr:last-child td{border-bottom:none}tr:hover td{background:#fafbfc}" +
        "th:not(:first-child),td:not(:first-child){text-align:right}" +
        "</style></head><body>";

    private static string H(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    private static string FormatDuration(TimeSpan ts)
        => ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s"
            : ts.TotalMinutes >= 1
                ? $"{ts.Minutes}m {ts.Seconds}s"
                : $"{ts.TotalSeconds:F1}s";

    private static object BuildUnified(MutationReportSummary pre, MutationReportSummary post)
    {
        var preMap = pre.Files.ToDictionary(f => f.SourceFile, StringComparer.OrdinalIgnoreCase);
        var postMap = post.Files.ToDictionary(f => f.SourceFile, StringComparer.OrdinalIgnoreCase);
        var allFiles = preMap.Keys.Union(postMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var preMutants = BuildUnifiedMutantRows(pre.Mutants);
        var postMutants = BuildUnifiedMutantRows(post.Mutants);

        var rows = new List<object>();
        foreach (var file in allFiles)
        {
            preMap.TryGetValue(file, out var preFile);
            postMap.TryGetValue(file, out var postFile);

            rows.Add(new
            {
                file,
                preScore = preFile?.Score ?? 0,
                postScore = postFile?.Score ?? 0,
                deltaScore = (postFile?.Score ?? 0) - (preFile?.Score ?? 0),
                preSurvived = preFile?.Survived ?? 0,
                postSurvived = postFile?.Survived ?? 0,
                deltaSurvived = (postFile?.Survived ?? 0) - (preFile?.Survived ?? 0)
            });
        }

        return new
        {
            generatedAtUtc = DateTime.UtcNow,
            pre = new { pre.ReportPath, pre.TotalKilled, pre.TotalSurvived, pre.TotalMutants, pre.Score },
            post = new { post.ReportPath, post.TotalKilled, post.TotalSurvived, post.TotalMutants, post.Score },
            overallDeltaScore = post.Score - pre.Score,
            files = rows,
            preMutants,
            postMutants
        };
    }

    private static string BuildMarkdown(object unified)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(unified));
        var root = doc.RootElement;

        var preScore = root.GetProperty("pre").GetProperty("Score").GetDouble();
        var postScore = root.GetProperty("post").GetProperty("Score").GetDouble();
        var delta = root.GetProperty("overallDeltaScore").GetDouble();

        var sb = new StringBuilder();
        sb.AppendLine("# Unified Mutation Report");
        sb.AppendLine();
        sb.AppendLine($"- Generated (UTC): {root.GetProperty("generatedAtUtc").GetDateTime():O}");
        sb.AppendLine($"- Pre-commit score: {preScore:F2}%");
        sb.AppendLine($"- Post-commit score: {postScore:F2}%");
        sb.AppendLine($"- Delta score: {delta:+0.00;-0.00;0.00}%");
        sb.AppendLine();
        sb.AppendLine("## File-Level Delta");
        sb.AppendLine();
        sb.AppendLine("| File | Pre Score | Post Score | Delta | Pre Survived | Post Survived | Delta Survived |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");

        foreach (var row in root.GetProperty("files").EnumerateArray())
        {
            sb.AppendLine($"| {row.GetProperty("file").GetString()} | " +
                         $"{row.GetProperty("preScore").GetDouble():F2}% | " +
                         $"{row.GetProperty("postScore").GetDouble():F2}% | " +
                         $"{row.GetProperty("deltaScore").GetDouble():+0.00;-0.00;0.00}% | " +
                         $"{row.GetProperty("preSurvived").GetInt32()} | " +
                         $"{row.GetProperty("postSurvived").GetInt32()} | " +
                         $"{row.GetProperty("deltaSurvived").GetInt32():+0;-0;0} |");
        }

        AppendMutantsMarkdownSection(sb, "Pre-commit Mutants", root.GetProperty("preMutants"));
        AppendMutantsMarkdownSection(sb, "Post-commit Mutants", root.GetProperty("postMutants"));

        return sb.ToString();
    }

    private static List<object> BuildUnifiedMutantRows(IReadOnlyList<MutationDetail> mutants)
        => mutants
            .OrderBy(m => m.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.StartLine ?? int.MaxValue)
            .ThenBy(m => m.StartColumn ?? int.MaxValue)
            .Select(m => (object)new
            {
                sourceFile = m.SourceFile,
                mutantId = m.MutantId,
                status = NormalizeMutantStatus(m.Status),
                mutatorName = string.IsNullOrWhiteSpace(m.MutatorName) ? "unknown" : m.MutatorName,
                location = FormatMutantLocation(m)
            })
            .ToList();

    private static string NormalizeMutantStatus(string? rawStatus)
    {
        var status = rawStatus ?? string.Empty;
        if (status.Equals("Killed", StringComparison.OrdinalIgnoreCase))
        {
            return "Killed";
        }

        if (status.Equals("Survived", StringComparison.OrdinalIgnoreCase))
        {
            return "Survived";
        }

        return "Skipped";
    }

    private static string FormatMutantLocation(MutationDetail mutant)
        => mutant.StartLine.HasValue
            ? $"{mutant.StartLine}:{mutant.StartColumn ?? 0}-{mutant.EndLine ?? mutant.StartLine}:{mutant.EndColumn ?? 0}"
            : "n/a";

    private static void AppendMutantsMarkdownSection(StringBuilder sb, string sectionTitle, JsonElement mutants)
    {
        var allMutants = mutants.EnumerateArray().ToList();
        var killedCount = allMutants.Count(m => (m.GetProperty("status").GetString() ?? string.Empty).Equals("Killed", StringComparison.OrdinalIgnoreCase));
        var survivedCount = allMutants.Count(m => (m.GetProperty("status").GetString() ?? string.Empty).Equals("Survived", StringComparison.OrdinalIgnoreCase));
        var skippedCount = allMutants.Count(m => (m.GetProperty("status").GetString() ?? string.Empty).Equals("Skipped", StringComparison.OrdinalIgnoreCase));

        sb.AppendLine();
        sb.AppendLine($"## {sectionTitle}");
        sb.AppendLine();
        sb.AppendLine($"- Total mutants: {allMutants.Count}");
        sb.AppendLine($"- Killed: {killedCount}");
        sb.AppendLine($"- Survived: {survivedCount}");
        sb.AppendLine($"- Skipped: {skippedCount}");
        sb.AppendLine();
        sb.AppendLine("| File | Mutant Id | Status | Mutator | Location |");
        sb.AppendLine("|---|---:|---|---|---|");

        foreach (var mutant in allMutants)
        {
            var file = mutant.GetProperty("sourceFile").GetString() ?? string.Empty;
            var id = mutant.GetProperty("mutantId").ValueKind == JsonValueKind.Null
                ? "n/a"
                : mutant.GetProperty("mutantId").GetInt32().ToString();
            var status = mutant.GetProperty("status").GetString() ?? string.Empty;
            var mutatorName = mutant.GetProperty("mutatorName").GetString() ?? "unknown";
            var location = mutant.GetProperty("location").GetString() ?? "n/a";
            sb.AppendLine($"| {file} | {id} | {status} | {mutatorName} | {location} |");
        }

        if (allMutants.Count == 0)
        {
            sb.AppendLine("| n/a | n/a | n/a | n/a | n/a |");
        }
    }
}
