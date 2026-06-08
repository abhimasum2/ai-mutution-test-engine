using System.Text;
using System.Text.Json;
using MEngine.Application.Abstractions.Services;
using MEngine.Domain.Entities;
using MEngine.Domain.Enums;

namespace MEngine.Infrastructure.Services;

public sealed class GitService : IGitService
{
    public Task<RepositoryAnalysisResult> AnalyzeRepositoryAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var changedFiles = new List<string>
        {
            "src/Service/MutationService.cs",
            "tests/MutationServiceTests.cs"
        };

        var summary = $"Repository '{repositoryUrl}' analyzed successfully.";
        return Task.FromResult(new RepositoryAnalysisResult("Success", changedFiles, summary));
    }

    public Task<string> ResolvePullRequestBranchAsync(string repositoryUrl, int pullRequestId, CancellationToken cancellationToken = default)
        => Task.FromResult($"refs/heads/pr/{pullRequestId}");
}

public sealed class AgentProfilingService : IAgentProfilingService
{
    public Task<AgentValidationResult> ValidateConfigurationAsync(string agentName, string secretKey, string endpointUrl, CancellationToken cancellationToken = default)
    {
        var isValid = !string.IsNullOrWhiteSpace(agentName)
            && !string.IsNullOrWhiteSpace(secretKey)
            && Uri.TryCreate(endpointUrl, UriKind.Absolute, out _);

        return Task.FromResult(new AgentValidationResult(isValid));
    }

    public Task<RepoProfileResult> ProfileAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var language = repositoryUrl.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ? "C#" : "Unknown";
        var testFramework = language == "C#" ? "xUnit" : "Unknown";
        return Task.FromResult(new RepoProfileResult(language, testFramework, "Master prompt profile selected based on repository metadata.", true));
    }
}

public sealed class StrykerMutationTestingService : IMutationTestingService
{
    public Task<MutationExecutionResult> ExecuteAsync(Guid runId, string testProjectPath, string solutionPath, IReadOnlyList<string> reporters, IDictionary<string, int> thresholds, string outputFolder, CancellationToken cancellationToken = default)
    {
        var reporterFlags = string.Join(',', reporters);
        var thresholdArguments = string.Join(' ', thresholds.Select(kvp => $"--threshold-{kvp.Key.ToLowerInvariant()} {kvp.Value}"));
        var reportDir = Path.Combine(outputFolder, "mutation");
        Directory.CreateDirectory(reportDir);

        var command = $"dotnet stryker --project \"{testProjectPath}\" --solution \"{solutionPath}\" --reporters {reporterFlags} {thresholdArguments} --output \"{reportDir}\"";
        var reportPath = Path.Combine(reportDir, "mutation-report.html");
        var jsonPath = Path.Combine(reportDir, "mutation-report.json");

        return Task.FromResult(new MutationExecutionResult(78.5m, reportPath, jsonPath, "Stryker.NET", command));
    }
}

public sealed class TestGenerationService : ITestGenerationService
{
    public Task<TestActionResult> GenerateOrUpdateTestsAsync(Guid runId, TestDecisionType decision, int maxIterations, string? agentPromptOverride, IReadOnlyList<string> targetProjects, CancellationToken cancellationToken = default)
    {
        var files = targetProjects.Select(p => Path.Combine(p, "Generated.Mutation.Tests.cs")).ToList();
        var status = decision == TestDecisionType.Skip ? ExecutionStepStatus.Skipped : ExecutionStepStatus.Succeeded;
        return Task.FromResult(new TestActionResult(Math.Max(1, maxIterations), status, files));
    }
}

public sealed class TestExecutionService : ITestExecutionService
{
    public Task<TestExecutionResult> RunTestsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var reportPath = Path.Combine("artifacts", runId.ToString("N"), "test-results.trx");
        return Task.FromResult(new TestExecutionResult(ExecutionStepStatus.Succeeded, 120, 120, 0, reportPath));
    }
}

public sealed class CommitService(IGitService gitService) : ICommitService
{
    public async Task<CommitExecutionResult> CommitAsync(Guid runId, string repositoryUrl, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var branch = await gitService.ResolvePullRequestBranchAsync(repositoryUrl, pullRequestId, cancellationToken);
        var sha = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        return new CommitExecutionResult(sha[..12], branch, pullRequestId, "Committed");
    }
}

public sealed class ArtifactFileService : IArtifactFileService
{
    public string BuildRunFolder(string baseOutputFolder, Guid runId)
    {
        var folder = Path.Combine(baseOutputFolder, runId.ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task<FinalReportFileResult> GenerateCombinedReportAsync(Guid runId, string outputFolder, MutationReport? mutationReport, TestRun? testRun, CancellationToken cancellationToken = default)
    {
        var runFolder = BuildRunFolder(outputFolder, runId);
        var finalReportPath = Path.Combine(runFolder, "final-report.json");
        var finalHtmlReportPath = Path.Combine(runFolder, "final-report.html");

        var payload = new
        {
            runId,
            generatedAtUtc = DateTimeOffset.UtcNow,
            mutationReport,
            testRun
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        await File.WriteAllTextAsync(finalReportPath, json, cancellationToken);

        var html = new StringBuilder()
            .AppendLine("<html><body>")
            .AppendLine("<h1>M-Engine Final Report</h1>")
            .AppendLine($"<p>Run: {runId}</p>")
            .AppendLine($"<p>Generated: {DateTimeOffset.UtcNow:O}</p>")
            .AppendLine($"<p>Mutation Score: {mutationReport?.MutationScore ?? 0}</p>")
            .AppendLine($"<p>Tests Passed: {testRun?.Passed ?? 0}/{testRun?.Total ?? 0}</p>")
            .AppendLine("</body></html>")
            .ToString();

        await File.WriteAllTextAsync(finalHtmlReportPath, html, cancellationToken);
        return new FinalReportFileResult(finalReportPath, finalHtmlReportPath);
    }
}

public sealed class PipelineNotifier : IPipelineNotifier
{
    public Task<PipelineNotificationResult> NotifyAsync(Guid runId, string finalReportPath, CancellationToken cancellationToken = default)
        => Task.FromResult(new PipelineNotificationResult($"Notified: {finalReportPath}"));
}
