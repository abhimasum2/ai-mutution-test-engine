using MEngine.Application.DTOs.AgentConfigurations;
using MEngine.Application.DTOs.Runs;

namespace MEngine.Application.Abstractions.Services;

public interface IOrchestrationService
{
    Task<ValidateAgentConfigurationResponse> ValidateAgentConfigurationAsync(ValidateAgentConfigurationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<CreateRunResponse> CreateRunAsync(CreateRunRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<RunStatusResponse> GetRunStatusAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<ProfileRunResponse> ProfileRunAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
    Task<RepositoryAnalysisResponse> AnalyzeRepositoryAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
    Task<RepositoryAnalysisResponse> GetLatestRepositoryAnalysisAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<MutationReportResponse> GenerateMutationReportAsync(Guid runId, GenerateMutationReportRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<MutationReportResponse> GetLatestMutationReportAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<TestDecisionResponse> DecideTestActionAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
    Task<TestActionResponse> ExecuteTestActionAsync(Guid runId, TestActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<TestRunResponse> ExecuteTestRunAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
    Task<TestRunResponse> GetTestRunAsync(Guid runId, Guid testRunId, CancellationToken cancellationToken = default);
    Task<CommitResponse> CommitAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
    Task<FinalReportResponse> GenerateFinalReportAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
    Task<FinalReportResponse> GetLatestFinalReportAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<PipelineNotificationResponse> NotifyPipelineAsync(Guid runId, string correlationId, CancellationToken cancellationToken = default);
}
