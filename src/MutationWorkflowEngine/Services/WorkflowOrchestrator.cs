using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class WorkflowOrchestrator(
    ProjectDiscoveryService discovery,
    GitService git,
    StrykerService stryker,
    OpenAiTestGenerationService ai,
    TestIntegrationService integration,
    ReportService reports)
{
    public async Task RunAsync(AppConfig config, CancellationToken cancellationToken)
    {
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
        var preReportPath = await stryker.RunMutationAsync(
            config.RepositoryRoot,
            config.TargetProjectPath,
            testProjectPath,
            changedFiles,
            preDir,
            "pre",
            timeout,
            cancellationToken);

        var preSummary = await stryker.ParseReportAsync(preReportPath, cancellationToken);
        Log(config, $"Pre-commit score: {preSummary.Score:F2}%");

        Log(config, "Generating test updates from mutation report with OpenAI...");
        var generationPlan = integration.BuildGenerationPlan(config.RepositoryRoot, testProjectPath, changedFiles);
        var patches = await ai.GenerateTestsAsync(config, framework, preSummary, generationPlan, cancellationToken);
        var updatedTests = await integration.ApplyGeneratedPatchesAsync(config.RepositoryRoot, patches, cancellationToken);
        Log(config, $"Updated/created test files: {updatedTests.Count}");

        if (config.CommitAndPush)
        {
            Log(config, "Committing and pushing generated tests...");
            await git.CommitAndPushAsync(
                config.RepositoryRoot,
                updatedTests,
                "test: AI-generated mutation coverage improvements",
                cancellationToken,
                timeout);
        }

        Log(config, "Running post-commit mutation testing...");
        var postReportPath = await stryker.RunMutationAsync(
            config.RepositoryRoot,
            config.TargetProjectPath,
            testProjectPath,
            changedFiles,
            postDir,
            "post",
            timeout,
            cancellationToken);

        var postSummary = await stryker.ParseReportAsync(postReportPath, cancellationToken);
        Log(config, $"Post-commit score: {postSummary.Score:F2}%");

        Log(config, "Creating unified report...");
        var (jsonPath, markdownPath) = await reports.WriteUnifiedReportAsync(config.ReportsDirectory, preSummary, postSummary, cancellationToken);

        Log(config, $"Unified report JSON: {jsonPath}");
        Log(config, $"Unified report Markdown: {markdownPath}");
    }

    private static void Log(AppConfig config, string message)
    {
        if (config.Verbose)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
