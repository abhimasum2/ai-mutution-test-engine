using System.Text.Json;
using MEngine.Application.Abstractions.Persistence;
using MEngine.Application.Abstractions.Services;
using MEngine.Application.Common;
using MEngine.Application.DTOs.AgentConfigurations;
using MEngine.Application.DTOs.Runs;
using MEngine.Domain.Entities;
using MEngine.Domain.Enums;

namespace MEngine.Application.Services;

public sealed class OrchestrationService(
    IAgentConfigurationRepository agentConfigurationRepository,
    IExecutionRunRepository executionRunRepository,
    IExecutionStepRepository executionStepRepository,
    IRepositoryAnalysisRepository repositoryAnalysisRepository,
    IMutationReportRepository mutationReportRepository,
    ITestDecisionRepository testDecisionRepository,
    ITestRunRepository testRunRepository,
    ICommitResultRepository commitResultRepository,
    IFinalReportRepository finalReportRepository,
    IPipelineNotificationRepository pipelineNotificationRepository,
    IAgentProfilingService agentProfilingService,
    IGitService gitService,
    IMutationTestingService mutationTestingService,
    ITestGenerationService testGenerationService,
    ITestExecutionService testExecutionService,
    ICommitService commitService,
    IArtifactFileService artifactFileService,
    IPipelineNotifier pipelineNotifier) : IOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ValidateAgentConfigurationResponse> ValidateAgentConfigurationAsync(
        ValidateAgentConfigurationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validation = await agentProfilingService.ValidateConfigurationAsync(request.AgentName, request.SecretKey, request.EndpointUrl, cancellationToken);

        var existing = await agentConfigurationRepository.GetByNameAsync(request.AgentName, cancellationToken);
        if (existing is null)
        {
            existing = new AgentConfiguration
            {
                AgentName = request.AgentName,
                SecretKey = request.SecretKey,
                EndpointUrl = request.EndpointUrl,
                IsValid = validation.IsValid,
                CorrelationId = correlationId
            };
            await agentConfigurationRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.SecretKey = request.SecretKey;
            existing.EndpointUrl = request.EndpointUrl;
            existing.IsValid = validation.IsValid;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            existing.CorrelationId = correlationId;
        }

        await agentConfigurationRepository.SaveChangesAsync(cancellationToken);

        return new ValidateAgentConfigurationResponse(validation.IsValid ? "OK" : "Failure");
    }

    public async Task<CreateRunResponse> CreateRunAsync(CreateRunRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var config = await agentConfigurationRepository.GetByIdAsync(request.AgentConfigurationId, cancellationToken)
            ?? throw new NotFoundException($"Agent configuration '{request.AgentConfigurationId}' was not found.");

        if (!config.IsValid)
        {
            throw new ConflictException("Agent configuration is not valid. Validate it before creating a run.");
        }

        var run = new ExecutionRun
        {
            RepositoryUrl = request.RepositoryUrl,
            PullRequestId = request.PullRequestId,
            AgentConfigurationId = request.AgentConfigurationId,
            SecretKey = request.SecretKey,
            MaxIterations = request.MaxIterations,
            OutputFolder = request.OutputFolder,
            NotifyPipeline = request.NotifyPipeline,
            Status = RunStatus.Pending,
            CorrelationId = correlationId
        };

        await executionRunRepository.AddAsync(run, cancellationToken);
        await executionRunRepository.SaveChangesAsync(cancellationToken);

        return new CreateRunResponse(run.Id, run.Status.ToString());
    }

    public async Task<RunStatusResponse> GetRunStatusAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);
        return new RunStatusResponse(run.Id, run.Status.ToString(), run.CurrentIteration, run.UpdatedAtUtc);
    }

    public async Task<ProfileRunResponse> ProfileRunAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);
        var latest = await repositoryAnalysisRepository.GetLatestByRunIdAsync(runId, cancellationToken);
        if (latest is not null && !string.IsNullOrWhiteSpace(latest.Language))
        {
            return new ProfileRunResponse(latest.Language, latest.TestFramework, latest.ProfileSummary, latest.MasterPromptApplied);
        }

        var profile = await agentProfilingService.ProfileAsync(run.RepositoryUrl, cancellationToken);
        var analysis = new RepositoryAnalysis
        {
            ExecutionRunId = runId,
            BuildStatus = "NotStarted",
            ChangedFilesJson = "[]",
            RepoSummary = "Repository profiling completed.",
            Language = profile.Language,
            TestFramework = profile.TestFramework,
            ProfileSummary = profile.ProfileSummary,
            MasterPromptApplied = profile.MasterPromptApplied,
            CorrelationId = correlationId
        };

        run.Status = RunStatus.InProgress;
        run.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await repositoryAnalysisRepository.AddAsync(analysis, cancellationToken);
        await repositoryAnalysisRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "Profile", ExecutionStepStatus.Succeeded, "Repository profiled and master prompt applied.", correlationId, cancellationToken);

        return new ProfileRunResponse(profile.Language, profile.TestFramework, profile.ProfileSummary, profile.MasterPromptApplied);
    }

    public async Task<RepositoryAnalysisResponse> AnalyzeRepositoryAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);
        var result = await gitService.AnalyzeRepositoryAsync(run.RepositoryUrl, cancellationToken);

        var entity = new RepositoryAnalysis
        {
            ExecutionRunId = runId,
            BuildStatus = result.BuildStatus,
            ChangedFilesJson = JsonSerializer.Serialize(result.ChangedFiles, JsonOptions),
            RepoSummary = result.RepoSummary,
            CorrelationId = correlationId
        };

        run.Status = result.BuildStatus.Equals("Success", StringComparison.OrdinalIgnoreCase)
            ? RunStatus.InProgress
            : RunStatus.Failed;
        run.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await repositoryAnalysisRepository.AddAsync(entity, cancellationToken);
        await repositoryAnalysisRepository.SaveChangesAsync(cancellationToken);

        await AddStepAsync(runId, "RepositoryAnalysis", result.BuildStatus.Equals("Success", StringComparison.OrdinalIgnoreCase) ? ExecutionStepStatus.Succeeded : ExecutionStepStatus.Failed, result.RepoSummary, correlationId, cancellationToken);

        return new RepositoryAnalysisResponse(result.BuildStatus, result.ChangedFiles, result.RepoSummary);
    }

    public async Task<RepositoryAnalysisResponse> GetLatestRepositoryAnalysisAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        _ = await GetRunOrThrowAsync(runId, cancellationToken);
        var analysis = await repositoryAnalysisRepository.GetLatestByRunIdAsync(runId, cancellationToken)
            ?? throw new NotFoundException("Repository analysis was not found for this run.");

        var changed = JsonSerializer.Deserialize<List<string>>(analysis.ChangedFilesJson, JsonOptions) ?? new List<string>();
        return new RepositoryAnalysisResponse(analysis.BuildStatus, changed, analysis.RepoSummary);
    }

    public async Task<MutationReportResponse> GenerateMutationReportAsync(Guid runId, GenerateMutationReportRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);
        var runFolder = artifactFileService.BuildRunFolder(run.OutputFolder, run.Id);

        var result = await mutationTestingService.ExecuteAsync(runId, request.TestProjectPath, request.SolutionPath, request.Reporters, request.Thresholds, runFolder, cancellationToken);

        var entity = new MutationReport
        {
            ExecutionRunId = runId,
            MutationScore = result.MutationScore,
            ReportPath = result.ReportPath,
            JsonReportPath = result.JsonReportPath,
            Tool = "Stryker.NET",
            ThresholdsJson = JsonSerializer.Serialize(request.Thresholds, JsonOptions),
            CorrelationId = correlationId
        };

        await mutationReportRepository.AddAsync(entity, cancellationToken);
        await mutationReportRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "MutationTesting", ExecutionStepStatus.Succeeded, $"Mutation command: {result.Command}", correlationId, cancellationToken);

        return new MutationReportResponse(entity.MutationScore, entity.ReportPath, entity.JsonReportPath, entity.Tool);
    }

    public async Task<MutationReportResponse> GetLatestMutationReportAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        _ = await GetRunOrThrowAsync(runId, cancellationToken);
        var report = await mutationReportRepository.GetLatestByRunIdAsync(runId, cancellationToken)
            ?? throw new NotFoundException("Mutation report was not found for this run.");

        return new MutationReportResponse(report.MutationScore, report.ReportPath, report.JsonReportPath, report.Tool);
    }

    public async Task<TestDecisionResponse> DecideTestActionAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        _ = await GetRunOrThrowAsync(runId, cancellationToken);

        var mutation = await mutationReportRepository.GetLatestByRunIdAsync(runId, cancellationToken);
        var analysis = await repositoryAnalysisRepository.GetLatestByRunIdAsync(runId, cancellationToken);

        var targetFiles = JsonSerializer.Deserialize<List<string>>(analysis?.ChangedFilesJson ?? "[]", JsonOptions) ?? new List<string>();

        var (decision, reason) = mutation switch
        {
            null => (TestDecisionType.ManualReviewRequired, "No mutation report available."),
            { MutationScore: < 60 } => (TestDecisionType.CreateTests, "Mutation score below 60. Create new tests."),
            { MutationScore: < 80 } => (TestDecisionType.UpdateTests, "Mutation score between 60 and 80. Update existing tests."),
            _ => (TestDecisionType.Skip, "Mutation score is acceptable.")
        };

        var entity = new TestDecision
        {
            ExecutionRunId = runId,
            Decision = decision,
            Reason = reason,
            TargetFilesJson = JsonSerializer.Serialize(targetFiles, JsonOptions),
            CorrelationId = correlationId
        };

        await testDecisionRepository.AddAsync(entity, cancellationToken);
        await testDecisionRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "TestDecision", ExecutionStepStatus.Succeeded, reason, correlationId, cancellationToken);

        return new TestDecisionResponse(decision, reason, targetFiles);
    }

    public async Task<TestActionResponse> ExecuteTestActionAsync(Guid runId, TestActionRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);

        var action = await testGenerationService.GenerateOrUpdateTestsAsync(
            runId,
            request.Decision,
            request.MaxIterations,
            request.AgentPromptOverride,
            request.TargetProjects,
            cancellationToken);

        run.CurrentIteration = action.Iteration;
        run.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await executionRunRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "TestActions", action.Status, "Tests generated/updated.", correlationId, cancellationToken);

        return new TestActionResponse(action.Iteration, action.Status.ToString(), action.GeneratedFiles);
    }

    public async Task<TestRunResponse> ExecuteTestRunAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);
        var result = await testExecutionService.RunTestsAsync(runId, cancellationToken);

        var entity = new TestRun
        {
            ExecutionRunId = runId,
            Iteration = run.CurrentIteration,
            Status = result.Status,
            Total = result.Total,
            Passed = result.Passed,
            Failed = result.Failed,
            ReportPath = result.ReportPath,
            CorrelationId = correlationId
        };

        run.Status = result.Status == ExecutionStepStatus.Succeeded ? RunStatus.InProgress : RunStatus.Failed;
        run.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await testRunRepository.AddAsync(entity, cancellationToken);
        await testRunRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "TestRun", result.Status, $"Passed: {result.Passed}/{result.Total}", correlationId, cancellationToken);

        return new TestRunResponse(entity.Id, result.Status.ToString(), result.Total, result.Passed, result.Failed, result.ReportPath);
    }

    public async Task<TestRunResponse> GetTestRunAsync(Guid runId, Guid testRunId, CancellationToken cancellationToken = default)
    {
        _ = await GetRunOrThrowAsync(runId, cancellationToken);
        var testRun = await testRunRepository.GetByIdAsync(testRunId, cancellationToken)
            ?? throw new NotFoundException($"Test run '{testRunId}' was not found.");

        if (testRun.ExecutionRunId != runId)
        {
            throw new NotFoundException($"Test run '{testRunId}' does not belong to run '{runId}'.");
        }

        return new TestRunResponse(testRun.Id, testRun.Status.ToString(), testRun.Total, testRun.Passed, testRun.Failed, testRun.ReportPath);
    }

    public async Task<CommitResponse> CommitAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);
        var latestTestRun = await testRunRepository.GetLatestByRunIdAsync(runId, cancellationToken)
            ?? throw new ConflictException("Cannot commit before at least one test run completes.");

        if (latestTestRun.Status != ExecutionStepStatus.Succeeded)
        {
            throw new ConflictException("Commit is allowed only when the latest test run passed.");
        }

        var result = await commitService.CommitAsync(runId, run.RepositoryUrl, run.PullRequestId, cancellationToken);

        var entity = new CommitResult
        {
            ExecutionRunId = runId,
            CommitSha = result.CommitSha,
            Branch = result.Branch,
            PullRequestId = result.PullRequestId,
            Status = result.Status,
            CorrelationId = correlationId
        };

        await commitResultRepository.AddAsync(entity, cancellationToken);
        await commitResultRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "Commit", ExecutionStepStatus.Succeeded, result.CommitSha, correlationId, cancellationToken);

        return new CommitResponse(entity.CommitSha, entity.Branch, entity.PullRequestId);
    }

    public async Task<FinalReportResponse> GenerateFinalReportAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        var run = await GetRunOrThrowAsync(runId, cancellationToken);

        var mutation = await mutationReportRepository.GetLatestByRunIdAsync(runId, cancellationToken);
        var testRun = await testRunRepository.GetLatestByRunIdAsync(runId, cancellationToken);
        var files = await artifactFileService.GenerateCombinedReportAsync(runId, run.OutputFolder, mutation, testRun, cancellationToken);

        var entity = new FinalReport
        {
            ExecutionRunId = runId,
            FinalReportPath = files.FinalReportPath,
            FinalHtmlReportPath = files.FinalHtmlReportPath,
            Summary = "Combined report generated.",
            CorrelationId = correlationId
        };

        run.Status = RunStatus.Completed;
        run.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await finalReportRepository.AddAsync(entity, cancellationToken);
        await finalReportRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "FinalReport", ExecutionStepStatus.Succeeded, files.FinalReportPath, correlationId, cancellationToken);

        return new FinalReportResponse(entity.FinalReportPath, entity.FinalHtmlReportPath);
    }

    public async Task<FinalReportResponse> GetLatestFinalReportAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        _ = await GetRunOrThrowAsync(runId, cancellationToken);
        var report = await finalReportRepository.GetLatestByRunIdAsync(runId, cancellationToken)
            ?? throw new NotFoundException("Final report was not found for this run.");

        return new FinalReportResponse(report.FinalReportPath, report.FinalHtmlReportPath);
    }

    public async Task<PipelineNotificationResponse> NotifyPipelineAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default)
    {
        _ = await GetRunOrThrowAsync(runId, cancellationToken);

        var report = await finalReportRepository.GetLatestByRunIdAsync(runId, cancellationToken)
            ?? throw new NotFoundException("Final report is required before pipeline notification.");

        var result = await pipelineNotifier.NotifyAsync(runId, report.FinalReportPath, cancellationToken);

        var entity = new PipelineNotification
        {
            ExecutionRunId = runId,
            NotificationStatus = result.NotificationStatus,
            Payload = report.FinalReportPath,
            CorrelationId = correlationId
        };

        await pipelineNotificationRepository.AddAsync(entity, cancellationToken);
        await pipelineNotificationRepository.SaveChangesAsync(cancellationToken);
        await AddStepAsync(runId, "PipelineNotification", ExecutionStepStatus.Succeeded, result.NotificationStatus, correlationId, cancellationToken);

        return new PipelineNotificationResponse(result.NotificationStatus);
    }

    private async Task<ExecutionRun> GetRunOrThrowAsync(Guid runId, CancellationToken cancellationToken)
    {
        return await executionRunRepository.GetByIdAsync(runId, cancellationToken)
            ?? throw new NotFoundException($"Run '{runId}' was not found.");
    }

    private async Task AddStepAsync(Guid runId, string stepName, ExecutionStepStatus status, string details, string correlationId, CancellationToken cancellationToken)
    {
        var step = new ExecutionStep
        {
            ExecutionRunId = runId,
            StepName = stepName,
            Status = status,
            Details = details,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = correlationId
        };

        await executionStepRepository.AddAsync(step, cancellationToken);
        await executionStepRepository.SaveChangesAsync(cancellationToken);
    }
}
