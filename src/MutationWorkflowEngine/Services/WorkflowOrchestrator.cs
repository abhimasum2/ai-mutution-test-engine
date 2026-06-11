using System.Diagnostics;
using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class WorkflowOrchestrator(
    ProjectDiscoveryService discovery,
    GitService git,
    StrykerService stryker,
    OpenAiTestGenerationService ai,
    TestIntegrationService integration,
    ReportService reports,
    ProcessRunner processRunner)
{
    public async Task RunAsync(AppConfig config, CancellationToken cancellationToken)
    {
        var totalTimer = Stopwatch.StartNew();
        var startedAtUtc = DateTime.UtcNow;
        var stageTimings = new List<PerformanceStageTiming>();

        var timeout = TimeSpan.FromMinutes(config.ProcessTimeoutMinutes);
        Directory.CreateDirectory(config.ReportsDirectory);

        Log(config, "Resolving test project and framework...");
        var (testProjectPath, framework) = await discovery.ResolveTestProjectAndFrameworkAsync(
            config.RepositoryRoot,
            config.TargetProjectPath,
            config.TestProjectPath,
            cancellationToken);

        Log(config, $"Detected framework: {framework}; test project: {testProjectPath}");

        Log(config, "Loading changed PR files using git diff...");
        var changedFiles = await git.GetChangedSourceFilesAsync(config.RepositoryRoot, config.BaseRef, cancellationToken, timeout);
        if (changedFiles.Count == 0)
        {
            throw new InvalidOperationException("No changed .cs source files found in PR diff.");
        }

        Log(config, $"Changed source files: {changedFiles.Count}");

        var preDir = Path.Combine(config.ReportsDirectory, "pre-commit");
        var postDir = Path.Combine(config.ReportsDirectory, "post-commit");

        Log(config, "Running pre-commit mutation testing...");
        var preStageTimer = Stopwatch.StartNew();
        var preReportPath = await stryker.RunMutationAsync(
            config.RepositoryRoot,
            config.TargetProjectPath,
            testProjectPath,
            changedFiles,
            preDir,
            "pre",
            timeout,
            cancellationToken);
        preStageTimer.Stop();
        stageTimings.Add(new PerformanceStageTiming("Pre-commit Mutation", preStageTimer.Elapsed));

        var preSummary = await stryker.ParseReportAsync(preReportPath, cancellationToken);
        Log(config, $"Pre-commit score: {preSummary.Score:F2}%");
        LogMutants(config, "Pre-commit", preSummary);

        var generationPlan = integration.BuildGenerationPlan(config.RepositoryRoot, testProjectPath, changedFiles);
        if (generationPlan.Count == 0)
        {
            throw new InvalidOperationException("No generation plan entries were created for changed files.");
        }

        IReadOnlyList<string> updatedTests = Array.Empty<string>();
        ProcessResult? lastBuildResult = null;
        var allTokenUsageRecords = new List<TokenUsageRecord>();

        var aiStageTimer = Stopwatch.StartNew();
        for (var attempt = 1; attempt <= config.GenerationMaxIterations; attempt++)
        {
            Log(config, $"Generating test updates from mutation report with AI (attempt {attempt}/{config.GenerationMaxIterations})...");
            var (patches, tokenUsage) = await ai.GenerateTestsAsync(config, framework, preSummary, generationPlan, cancellationToken);
            allTokenUsageRecords.AddRange(tokenUsage.Records);
            Log(config, $"Tokens used this attempt — input: {tokenUsage.TotalInputTokens}, output: {tokenUsage.TotalOutputTokens}");
            updatedTests = await integration.ApplyGeneratedPatchesAsync(config.RepositoryRoot, patches, cancellationToken);
            Log(config, $"Updated/created test files: {updatedTests.Count}");

            Log(config, "Validating test project build...");
            lastBuildResult = await processRunner.RunAsync(
                "dotnet",
                $"build \"{testProjectPath}\" --nologo",
                config.RepositoryRoot,
                timeout,
                cancellationToken);

            if (lastBuildResult.IsSuccess)
            {
                Log(config, "Test project build passed.");
                break;
            }

            Log(config, $"Test project build failed on attempt {attempt}. Regenerating tests...");
            if (attempt == config.GenerationMaxIterations)
            {
                var buildDetails = string.IsNullOrWhiteSpace(lastBuildResult.StdErr)
                    ? lastBuildResult.StdOut
                    : lastBuildResult.StdErr;

                throw new InvalidOperationException(
                    "Generated tests did not build after all attempts. Last build output:\n" + buildDetails);
            }
        }
        aiStageTimer.Stop();
        stageTimings.Add(new PerformanceStageTiming("AI Test Generation", aiStageTimer.Elapsed));

        if (config.CommitAndPush)
        {
            Log(config, "Committing and pushing generated tests...");
            var commitStageTimer = Stopwatch.StartNew();
            await git.CommitAndPushAsync(
                config.RepositoryRoot,
                updatedTests,
                "test: AI-generated mutation coverage improvements",
                cancellationToken,
                timeout);
            commitStageTimer.Stop();
            stageTimings.Add(new PerformanceStageTiming("Commit and Push", commitStageTimer.Elapsed));
        }

        Log(config, "Running post-commit mutation testing...");
        var postStageTimer = Stopwatch.StartNew();
        var postReportPath = await stryker.RunMutationAsync(
            config.RepositoryRoot,
            config.TargetProjectPath,
            testProjectPath,
            changedFiles,
            postDir,
            "post",
            timeout,
            cancellationToken);
        postStageTimer.Stop();
        stageTimings.Add(new PerformanceStageTiming("Post-commit Mutation", postStageTimer.Elapsed));

        var postSummary = await stryker.ParseReportAsync(postReportPath, cancellationToken);
        Log(config, $"Post-commit score: {postSummary.Score:F2}%");
        LogMutants(config, "Post-commit", postSummary);

        Log(config, "Creating unified report...");
        var (jsonPath, markdownPath) = await reports.WriteUnifiedReportAsync(config.ReportsDirectory, preSummary, postSummary, cancellationToken);

        Log(config, $"Unified report JSON: {jsonPath}");
        Log(config, $"Unified report Markdown: {markdownPath}");

        Log(config, "Creating token usage report...");
        var finalTokenReport = new TokenUsageReport(
            allTokenUsageRecords.Sum(r => r.InputTokens),
            allTokenUsageRecords.Sum(r => r.OutputTokens),
            allTokenUsageRecords.Sum(r => r.TotalTokens),
            allTokenUsageRecords);
        var tokenReportPath = await reports.WriteTokenUsageReportAsync(config.ReportsDirectory, finalTokenReport, cancellationToken);
        Log(config, $"Token usage report: {tokenReportPath}");
        Log(config, $"Total tokens consumed — input: {finalTokenReport.TotalInputTokens}, output: {finalTokenReport.TotalOutputTokens}, total: {finalTokenReport.TotalTokens}");

        totalTimer.Stop();
        var performanceReport = new PerformanceReport(startedAtUtc, DateTime.UtcNow, totalTimer.Elapsed, stageTimings);
        var perfReportPath = await reports.WritePerformanceReportAsync(config.ReportsDirectory, performanceReport, cancellationToken);
        Log(config, $"Performance report: {perfReportPath}");
        Log(config, $"Total engine runtime: {totalTimer.Elapsed:hh\\:mm\\:ss}");

        Log(config, "Creating combined summary HTML...");
        var htmlPath = await reports.WriteSummaryHtmlAsync(config.ReportsDirectory, preSummary, postSummary, finalTokenReport, performanceReport, cancellationToken);
        Log(config, $"Summary HTML: {htmlPath}");
    }

    private static void Log(AppConfig config, string message)
    {
        if (config.Verbose)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }

    private static void LogMutants(AppConfig config, string phase, MutationReportSummary summary)
    {
        var tracked = summary.Mutants
            .Where(m => m.Status.Equals("Killed", StringComparison.OrdinalIgnoreCase)
                || m.Status.Equals("Survived", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Log(config, $"{phase} mutants identified: {tracked.Count}");

        foreach (var file in summary.Files.OrderBy(f => f.SourceFile, StringComparer.OrdinalIgnoreCase))
        {
            Log(config, $"{phase} file: {file.SourceFile} | killed={file.Killed}, survived={file.Survived}, total={file.Total}");
        }

        foreach (var mutant in tracked)
        {
            var idText = mutant.MutantId.HasValue ? mutant.MutantId.Value.ToString() : "n/a";
            var mutatorText = string.IsNullOrWhiteSpace(mutant.MutatorName) ? "unknown" : mutant.MutatorName;
            var locationText = mutant.StartLine.HasValue
                ? $"{mutant.StartLine}:{mutant.StartColumn ?? 0}-{mutant.EndLine ?? mutant.StartLine}:{mutant.EndColumn ?? 0}"
                : "n/a";

            Log(config, $"{phase} mutant id={idText} status={mutant.Status} file={mutant.SourceFile} mutator={mutatorText} location={locationText}");
        }
    }
}
