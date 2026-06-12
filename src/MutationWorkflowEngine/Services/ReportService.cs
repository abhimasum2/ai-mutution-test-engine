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

        var preCounts = GetStatusCounts(pre.Mutants);
        var postCounts = GetStatusCounts(post.Mutants);
        var mutantRows = new StringBuilder();
        mutantRows.Append(BuildMutantHtmlRows(pre.Mutants, "Pre-commit"));
        mutantRows.Append(BuildMutantHtmlRows(post.Mutants, "Post-commit"));

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
            .AppendLine("<div class=\"section\"><h2>Unified Mutation Report Mutants</h2>")
            .AppendLine("<div class=\"w-100\"><div class=\"w-50\">")
            .AppendLine("<div class=\"summary-section\">")
            .AppendLine("<div class=\"section-title\">Pre-commit</div>")
            .AppendLine("<div class=\"card-grid\">")
            .AppendLine($"<div class=\"metric-card total\"><h4>Total</h4><p>{pre.Mutants.Count}</p></div>")
            .AppendLine($"<div class=\"metric-card killed\"><h4>Killed</h4><p>{preCounts.Killed}</p></div>")
            .AppendLine($"<div class=\"metric-card survived\"><h4>Survived</h4><p>{preCounts.Survived}</p></div>")
            .AppendLine($"<div class=\"metric-card skipped\"><h4>Skipped</h4><p>{preCounts.Skipped}</p></div>")
            .AppendLine($"<div class=\"metric-card score\"><h4>Score</h4><p>{pre.Score:F1}%</p></div>")
            .AppendLine("</div>")
            .AppendLine("</div>")
            .AppendLine("<div class=\"summary-section\">")
            .AppendLine("<div class=\"section-title\">Post-commit</div>")
            .AppendLine("<div class=\"card-grid\">")
            .AppendLine($"<div class=\"metric-card total\"><h4>Total</h4><p>{post.Mutants.Count}</p></div>")
            .AppendLine($"<div class=\"metric-card killed\"><h4>Killed</h4><p>{postCounts.Killed}</p></div>")
            .AppendLine($"<div class=\"metric-card survived\"><h4>Survived</h4><p>{postCounts.Survived}</p></div>")
            .AppendLine($"<div class=\"metric-card skipped\"><h4>Skipped</h4><p>{postCounts.Skipped}</p></div>")
            .AppendLine($"<div class=\"metric-card score\"><h4>Score</h4><p>{post.Score:F1}%</p></div>")
            .AppendLine("</div>")
            .AppendLine("</div>")
            .AppendLine("</div>")
            .AppendLine("<div class=\"w-50\">")
            .AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>")
            .AppendLine("<div class=\"chart-container\">")
            .AppendLine("<canvas id=\"mutationSummaryChart\"></canvas>")
            .AppendLine("</div>")

            .AppendLine("<script>")
            .AppendLine("const ctx = document.getElementById('mutationSummaryChart').getContext('2d');")
            .AppendLine("new Chart(ctx, {")
            .AppendLine("  type: 'bar',")
            .AppendLine("  data: {")
            .AppendLine("    labels: ['Total', 'Killed', 'Survived', 'Skipped', 'Score'],")
            .AppendLine("    datasets: [")
            .AppendLine("      {")
            .AppendLine("        label: 'Pre-commit',")
            .AppendLine($"        data: [{pre.Mutants.Count}, {preCounts.Killed}, {preCounts.Survived}, {preCounts.Skipped}, {pre.Score:F1}],")
            .AppendLine("        backgroundColor: 'rgba(0, 120, 212, 0.7)',")
            .AppendLine("        borderColor: 'rgba(0, 120, 212, 1)',")
            .AppendLine("        borderWidth: 1")
            .AppendLine("      },")
            .AppendLine("      {")
            .AppendLine("        label: 'Post-commit',")
            .AppendLine($"        data: [{post.Mutants.Count}, {postCounts.Killed}, {postCounts.Survived}, {postCounts.Skipped}, {post.Score:F1}],")
            .AppendLine("        backgroundColor: 'rgba(40, 167, 69, 0.7)',")
            .AppendLine("        borderColor: 'rgba(40, 167, 69, 1)',")
            .AppendLine("        borderWidth: 1")
            .AppendLine("      }")
            .AppendLine("    ]")
            .AppendLine("  },")
            .AppendLine("  options: {")
            .AppendLine("    responsive: true,")
            .AppendLine("    plugins: {")
            .AppendLine("      legend: { position: 'top' },")
            .AppendLine("      title: { display: true, text: 'Mutation Summary Comparison' }")
            .AppendLine("    },")
            .AppendLine("    scales: {")
            .AppendLine("      y: { beginAtZero: true, ticks: { precision: 0 } }")
            .AppendLine("    }")
            .AppendLine("  }")
            .AppendLine("});")
            .AppendLine("</script>")
            .AppendLine("</div></div>")

            .AppendLine("<div class=\"section-title\">Mutant(s) List</div>")
            .AppendLine("<div class=\"filters\">")
            .AppendLine("<div class=\"filter-item\">")
            .AppendLine("<label for=\"mutantPhaseFilter\">Phase</label>")
            .AppendLine("<select id=\"mutantPhaseFilter\"><option value=\"all\">All</option><option value=\"pre-commit\">Pre-commit</option><option value=\"post-commit\">Post-commit</option></select>")
            .AppendLine("</div>")
            .AppendLine("<div class=\"filter-item\">")
            .AppendLine("<label for=\"mutantStatusFilter\">Status</label>")
            .AppendLine("<select id=\"mutantStatusFilter\"><option value=\"all\">All</option><option value=\"killed\">Killed</option><option value=\"survived\">Survived</option><option value=\"skipped\">Skipped</option></select>")
            .AppendLine("</div>")
            .AppendLine("<div class=\"filter-item\">")
            .AppendLine("<label for=\"mutantFileFilter\">File contains</label>")
            .AppendLine("<input id=\"mutantFileFilter\" type=\"text\" placeholder=\"e.g. MutationTestingSample\" />")
            .AppendLine("</div>")
            .AppendLine("<div class=\"filter-item\">")
            .AppendLine("<label for=\"mutantMutatorFilter\">Mutator contains</label>")
            .AppendLine("<input id=\"mutantMutatorFilter\" type=\"text\" placeholder=\"e.g. Equality mutation\" />")
            .AppendLine("</div>")
            .AppendLine("<div class=\"filter-item\">")
            .AppendLine("<label for=\"mutantPageSize\">Page size</label>")
            .AppendLine("<select id=\"mutantPageSize\"><option value=\"10\">10</option><option value=\"25\" selected>25</option><option value=\"50\">50</option><option value=\"100\">100</option></select>")
            .AppendLine("</div></div>")
            .AppendLine("<p class=\"meta\" id=\"mutantFilterSummary\">Showing all mutants</p>")
            .AppendLine("<table id=\"mutantsTable\"><thead><tr><th>Phase</th><th>File</th><th>Line</th><th>Status</th><th>Mutator</th><th>Location</th></tr></thead><tbody id=\"mutantsTableBody\">")
            .Append(mutantRows)
            .AppendLine("</tbody></table></div>")
            .AppendLine("<div class=\"pager\">")
            .AppendLine("<button id=\"mutantPrevPage\" type=\"button\">Previous</button>")
            .AppendLine("<span id=\"mutantPageInfo\">Page 1 of 1</span>")
            .AppendLine("<button id=\"mutantNextPage\" type=\"button\">Next</button>")
            .AppendLine("</div>")
            .AppendLine("<script>")
            .AppendLine("(function(){")
            .AppendLine("  const phase = document.getElementById('mutantPhaseFilter');")
            .AppendLine("  const status = document.getElementById('mutantStatusFilter');")
            .AppendLine("  const file = document.getElementById('mutantFileFilter');")
            .AppendLine("  const mutator = document.getElementById('mutantMutatorFilter');")
            .AppendLine("  const pageSize = document.getElementById('mutantPageSize');")
            .AppendLine("  const prevPage = document.getElementById('mutantPrevPage');")
            .AppendLine("  const nextPage = document.getElementById('mutantNextPage');")
            .AppendLine("  const pageInfo = document.getElementById('mutantPageInfo');")
            .AppendLine("  const summary = document.getElementById('mutantFilterSummary');")
            .AppendLine("  const rows = Array.from(document.querySelectorAll('#mutantsTableBody tr'));")
            .AppendLine("  let filteredRows = rows;")
            .AppendLine("  let currentPage = 1;")
            .AppendLine("  function renderPage(){")
            .AppendLine("    const size = Math.max(1, parseInt(pageSize.value || '25', 10));")
            .AppendLine("    const totalPages = Math.max(1, Math.ceil(filteredRows.length / size));")
            .AppendLine("    if (currentPage > totalPages) currentPage = totalPages;")
            .AppendLine("    if (currentPage < 1) currentPage = 1;")
            .AppendLine("    const start = (currentPage - 1) * size;")
            .AppendLine("    const end = start + size;")
            .AppendLine("    rows.forEach(r => r.style.display = 'none');")
            .AppendLine("    filteredRows.slice(start, end).forEach(r => r.style.display = '');")
            .AppendLine("    summary.textContent = 'Showing ' + filteredRows.length + ' mutant(s)';")
            .AppendLine("    pageInfo.textContent = 'Page ' + currentPage + ' of ' + totalPages;")
            .AppendLine("    prevPage.disabled = currentPage <= 1;")
            .AppendLine("    nextPage.disabled = currentPage >= totalPages;")
            .AppendLine("  }")
            .AppendLine("  function applyFilters(){")
            .AppendLine("    const phaseValue = (phase.value || 'all').toLowerCase();")
            .AppendLine("    const statusValue = (status.value || 'all').toLowerCase();")
            .AppendLine("    const fileValue = (file.value || '').trim().toLowerCase();")
            .AppendLine("    const mutatorValue = (mutator.value || '').trim().toLowerCase();")
            .AppendLine("    filteredRows = rows.filter(row => {")
            .AppendLine("      const rowPhase = row.dataset.phase || '';")
            .AppendLine("      const rowStatus = row.dataset.status || '';")
            .AppendLine("      const rowFile = row.dataset.file || '';")
            .AppendLine("      const rowMutator = row.dataset.mutator || '';")
            .AppendLine("      const matchesPhase = phaseValue === 'all' || rowPhase === phaseValue;")
            .AppendLine("      const matchesStatus = statusValue === 'all' || rowStatus === statusValue;")
            .AppendLine("      const matchesFile = !fileValue || rowFile.includes(fileValue);")
            .AppendLine("      const matchesMutator = !mutatorValue || rowMutator.includes(mutatorValue);")
            .AppendLine("      return matchesPhase && matchesStatus && matchesFile && matchesMutator;")
            .AppendLine("    });")
            .AppendLine("    currentPage = 1;")
            .AppendLine("    renderPage();")
            .AppendLine("  }")
            .AppendLine("  [phase, status, file, mutator, pageSize].forEach(el => el.addEventListener('input', applyFilters));")
            .AppendLine("  [phase, status, pageSize].forEach(el => el.addEventListener('change', applyFilters));")
            .AppendLine("  prevPage.addEventListener('click', function(){ currentPage--; renderPage(); });")
            .AppendLine("  nextPage.addEventListener('click', function(){ currentPage++; renderPage(); });")
            .AppendLine("  applyFilters();")
            .AppendLine("})();")
            .AppendLine("</script>")
            .Append("</body></html>");

        var htmlPath = Path.Combine(reportsDirectory, "MutationSummary.html");
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
        ".w-100{width:100%;float:left}" +
        ".w-50{width:50%;float:left;}" +
        ".summary-section {margin-bottom: 20px; }" +
        ".section-title {font-weight: 600; margin: 10px 0; }" +
        ".card-grid {display: flex; gap: 10px; flex-wrap: wrap; }" +
        ".metric-card {min-width: 100px;padding: 15px;box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);}" +
        ".metric-card h4 { margin: 0; font-size: 12px;color: #555;font-weight: 500;}" +
        ".metric-card p { margin: 4px 0 0; font-size: 16px; font-weight: bold; }" +
        ".total { border-top: 4px solid #3b82f6; }" +
        ".killed {border-top: 4px solid #22c55e; }" +
        ".survived {border-top: 4px solid #d83b01; }" +
        ".skipped {border-top: 4px solid #605e5c; }" +
        ".score {border-top: 4px solid #f8ab03; }" +
        ".chart-container { width: 900px; max-width: 100%; margin: 20px 0; }" +
        ".filters{display:flex;flex-wrap:wrap;gap:12px}" +
        ".filter-item{display:flex;flex-direction:column;font-size:12px;min-width:140px}" +
        ".filter-item label{margin-bottom:4px;font-weight:500;color: #555}" +
        ".filter-item select,.filter-item input{padding:4px 6px;font-size:12px}" +
        ".pager{display:flex;align-items:center;gap:10px;margin:-8px 0 20px}" +
        ".pager button{padding:6px 10px;border:1px solid #d7dce2;border-radius:6px;background:#fff;color:#222;cursor:pointer}" +
        ".pager button:disabled{opacity:.5;cursor:not-allowed}" +
        "#mutantPageInfo{font-size:.85em;color:#555}" +
        "table{width:100%;border-collapse:collapse;font-size:.85em}" +
        "th{background:#f8f9fa;padding:8px 12px;text-align:left;color:#666;font-weight:600;border-bottom:2px solid #e9ecef}" +
        "td{padding:7px 12px;border-bottom:1px solid #f0f0f0}" +
        "tr:last-child td{border-bottom:none}tr:hover td{background:#fafbfc}" +
        "th:not(:first-child),td:not(:first-child){text-align:left}" +
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

        AppendMutantsMarkdownSection(sb, "Pre-commit Mutants", root.GetProperty("preMutants"), preScore.ToString("F2"), postScore.ToString("F2"));
        AppendMutantsMarkdownSection(sb, "Post-commit Mutants", root.GetProperty("postMutants"), preScore.ToString("F2"), postScore.ToString("F2"));

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

    private static void AppendMutantsMarkdownSection(StringBuilder sb, string sectionTitle, JsonElement mutants, string preScore, string postScore)
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
        sb.AppendLine($"- Pre-Score: {preScore}");
        sb.AppendLine($"- Post-Score: {postScore}");
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

    private static (int Killed, int Survived, int Skipped) GetStatusCounts(IReadOnlyList<MutationDetail> mutants)
    {
        var killed = 0;
        var survived = 0;
        var skipped = 0;

        foreach (var mutant in mutants)
        {
            var status = NormalizeMutantStatus(mutant.Status);
            if (status.Equals("Killed", StringComparison.OrdinalIgnoreCase))
            {
                killed++;
            }
            else if (status.Equals("Survived", StringComparison.OrdinalIgnoreCase))
            {
                survived++;
            }
            else
            {
                skipped++;
            }
        }

        return (killed, survived, skipped);
    }

    private static string BuildMutantHtmlRows(IReadOnlyList<MutationDetail> mutants, string phase)
    {
        var sb = new StringBuilder();
        var normalizedPhase = phase.ToLowerInvariant();

        foreach (var mutant in mutants
            .OrderBy(m => m.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.StartLine ?? int.MaxValue)
            .ThenBy(m => m.StartColumn ?? int.MaxValue))
        {
            var status = NormalizeMutantStatus(mutant.Status);
            var file = mutant.SourceFile;
            var lineNumber = mutant.StartLine?.ToString() ?? "n/a";
            var mutator = string.IsNullOrWhiteSpace(mutant.MutatorName) ? "unknown" : mutant.MutatorName;
            var location = FormatMutantLocation(mutant);

            sb.Append("<tr")
                .Append($" data-phase=\"{H(normalizedPhase)}\"")
                .Append($" data-status=\"{H(status.ToLowerInvariant())}\"")
                .Append($" data-file=\"{H(file.ToLowerInvariant())}\"")
                .Append($" data-mutator=\"{H(mutator.ToLowerInvariant())}\"")
                .Append(">")
                .Append($"<td>{H(phase)}</td>")
                .Append($"<td>{H(file)}</td>")
                .Append($"<td>{H(lineNumber)}</td>")
                .Append($"<td>{H(status)}</td>")
                .Append($"<td>{H(mutator)}</td>")
                .Append($"<td>{H(location)}</td>")
                .AppendLine("</tr>");
        }

        return sb.ToString();
    }
}
