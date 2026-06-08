using MEngine.Domain.Entities;
using MEngine.Domain.Enums;

namespace MEngine.Application.Abstractions.Services;

public interface IGitService
{
    Task<RepositoryAnalysisResult> AnalyzeRepositoryAsync(string repositoryUrl, CancellationToken cancellationToken = default);
    Task<string> ResolvePullRequestBranchAsync(string repositoryUrl, int pullRequestId, CancellationToken cancellationToken = default);
}

public interface IAgentProfilingService
{
    Task<AgentValidationResult> ValidateConfigurationAsync(string agentName, string secretKey, string endpointUrl, CancellationToken cancellationToken = default);
    Task<RepoProfileResult> ProfileAsync(string repositoryUrl, CancellationToken cancellationToken = default);
}

public interface IMutationTestingService
{
    Task<MutationExecutionResult> ExecuteAsync(Guid runId, string testProjectPath, string solutionPath, IReadOnlyList<string> reporters, IDictionary<string, int> thresholds, string outputFolder, CancellationToken cancellationToken = default);
}

public interface ITestGenerationService
{
    Task<TestActionResult> GenerateOrUpdateTestsAsync(Guid runId, TestDecisionType decision, int maxIterations, string? agentPromptOverride, IReadOnlyList<string> targetProjects, CancellationToken cancellationToken = default);
}

public interface ITestExecutionService
{
    Task<TestExecutionResult> RunTestsAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface ICommitService
{
    Task<CommitExecutionResult> CommitAsync(Guid runId, string repositoryUrl, int pullRequestId, CancellationToken cancellationToken = default);
}

public interface IArtifactFileService
{
    string BuildRunFolder(string baseOutputFolder, Guid runId);
    Task<FinalReportFileResult> GenerateCombinedReportAsync(Guid runId, string outputFolder, MutationReport? mutationReport, TestRun? testRun, CancellationToken cancellationToken = default);
}

public interface IPipelineNotifier
{
    Task<PipelineNotificationResult> NotifyAsync(Guid runId, string finalReportPath, CancellationToken cancellationToken = default);
}
