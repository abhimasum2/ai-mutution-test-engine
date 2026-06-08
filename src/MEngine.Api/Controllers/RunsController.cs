using Microsoft.AspNetCore.Mvc;
using MEngine.Application.Abstractions.Services;
using MEngine.Application.DTOs.Runs;

namespace MEngine.Api.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class RunsController(IOrchestrationService orchestrationService) : ControllerBase
{
    /// <summary>
    /// Creates a new orchestration run.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateRunResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateRunResponse>> CreateRunAsync([FromBody] CreateRunRequest request, CancellationToken cancellationToken)
    {
        var response = await orchestrationService.CreateRunAsync(request, GetCorrelationId(), cancellationToken);
        return CreatedAtAction(nameof(GetRunStatusAsync), new { runId = response.RunId }, response);
    }

    /// <summary>
    /// Gets the current status for a run.
    /// </summary>
    [HttpGet("{runId:guid}")]
    [ProducesResponseType(typeof(RunStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RunStatusResponse>> GetRunStatusAsync(Guid runId, CancellationToken cancellationToken)
        => Ok(await orchestrationService.GetRunStatusAsync(runId, cancellationToken));

    /// <summary>
    /// Profiles repository language and test framework and applies the master prompt profile.
    /// </summary>
    [HttpPost("{runId:guid}/profile")]
    [ProducesResponseType(typeof(ProfileRunResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ProfileRunResponse>> ProfileAsync(Guid runId, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.ProfileRunAsync(runId, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Builds and analyzes repository status and changed files.
    /// </summary>
    [HttpPost("{runId:guid}/repository-analysis")]
    [ProducesResponseType(typeof(RepositoryAnalysisResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<RepositoryAnalysisResponse>> AnalyzeRepositoryAsync(Guid runId, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.AnalyzeRepositoryAsync(runId, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Fetches the latest repository analysis.
    /// </summary>
    [HttpGet("{runId:guid}/repository-analysis")]
    [ProducesResponseType(typeof(RepositoryAnalysisResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RepositoryAnalysisResponse>> GetRepositoryAnalysisAsync(Guid runId, CancellationToken cancellationToken)
        => Ok(await orchestrationService.GetLatestRepositoryAnalysisAsync(runId, cancellationToken));

    /// <summary>
    /// Generates mutation report using Stryker.NET.
    /// </summary>
    [HttpPost("{runId:guid}/mutation-reports")]
    [ProducesResponseType(typeof(MutationReportResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MutationReportResponse>> GenerateMutationReportAsync(Guid runId, [FromBody] GenerateMutationReportRequest request, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.GenerateMutationReportAsync(runId, request, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Fetches latest mutation report metadata.
    /// </summary>
    [HttpGet("{runId:guid}/mutation-reports/latest")]
    [ProducesResponseType(typeof(MutationReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MutationReportResponse>> GetLatestMutationReportAsync(Guid runId, CancellationToken cancellationToken)
        => Ok(await orchestrationService.GetLatestMutationReportAsync(runId, cancellationToken));

    /// <summary>
    /// Decides whether to create, update, skip tests, or request manual review.
    /// </summary>
    [HttpPost("{runId:guid}/test-decision")]
    [ProducesResponseType(typeof(TestDecisionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestDecisionResponse>> DecideTestActionAsync(Guid runId, CancellationToken cancellationToken)
        => Ok(await orchestrationService.DecideTestActionAsync(runId, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Generates or updates tests based on the decision.
    /// </summary>
    [HttpPost("{runId:guid}/tests/actions")]
    [ProducesResponseType(typeof(TestActionResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<TestActionResponse>> ExecuteTestActionsAsync(Guid runId, [FromBody] TestActionRequest request, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.ExecuteTestActionAsync(runId, request, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Executes tests and stores test run results.
    /// </summary>
    [HttpPost("{runId:guid}/test-runs")]
    [ProducesResponseType(typeof(TestRunResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<TestRunResponse>> ExecuteTestRunAsync(Guid runId, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.ExecuteTestRunAsync(runId, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Fetches a specific test run result.
    /// </summary>
    [HttpGet("{runId:guid}/test-runs/{testRunId:guid}")]
    [ProducesResponseType(typeof(TestRunResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestRunResponse>> GetTestRunAsync(Guid runId, Guid testRunId, CancellationToken cancellationToken)
        => Ok(await orchestrationService.GetTestRunAsync(runId, testRunId, cancellationToken));

    /// <summary>
    /// Commits and pushes successful test changes to the same PR source branch.
    /// </summary>
    [HttpPost("{runId:guid}/commits")]
    [ProducesResponseType(typeof(CommitResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<CommitResponse>> CommitAsync(Guid runId, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.CommitAsync(runId, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Generates final combined report artifacts in local output folder.
    /// </summary>
    [HttpPost("{runId:guid}/final-reports")]
    [ProducesResponseType(typeof(FinalReportResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<FinalReportResponse>> GenerateFinalReportAsync(Guid runId, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.GenerateFinalReportAsync(runId, GetCorrelationId(), cancellationToken));

    /// <summary>
    /// Fetches latest final report metadata.
    /// </summary>
    [HttpGet("{runId:guid}/final-reports/latest")]
    [ProducesResponseType(typeof(FinalReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FinalReportResponse>> GetLatestFinalReportAsync(Guid runId, CancellationToken cancellationToken)
        => Ok(await orchestrationService.GetLatestFinalReportAsync(runId, cancellationToken));

    /// <summary>
    /// Notifies pipeline with final report artifact path.
    /// </summary>
    [HttpPost("{runId:guid}/pipeline-notifications")]
    [ProducesResponseType(typeof(PipelineNotificationResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<PipelineNotificationResponse>> NotifyPipelineAsync(Guid runId, CancellationToken cancellationToken)
        => Accepted(await orchestrationService.NotifyPipelineAsync(runId, GetCorrelationId(), cancellationToken));

    private string GetCorrelationId()
        => Request.Headers.TryGetValue("X-Correlation-ID", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : HttpContext.TraceIdentifier;
}
