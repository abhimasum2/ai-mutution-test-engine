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

    private static object BuildUnified(MutationReportSummary pre, MutationReportSummary post)
    {
        var preMap = pre.Files.ToDictionary(f => f.SourceFile, StringComparer.OrdinalIgnoreCase);
        var postMap = post.Files.ToDictionary(f => f.SourceFile, StringComparer.OrdinalIgnoreCase);
        var allFiles = preMap.Keys.Union(postMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

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
            files = rows
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

        return sb.ToString();
    }
}
