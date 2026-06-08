using MEngine.Domain.Enums;

namespace MEngine.Application.Abstractions.Services;

public sealed record AgentValidationResult(bool IsValid);

public sealed record RepoProfileResult(string Language, string TestFramework, string ProfileSummary, bool MasterPromptApplied);

public sealed record RepositoryAnalysisResult(string BuildStatus, IReadOnlyList<string> ChangedFiles, string RepoSummary);

public sealed record MutationExecutionResult(decimal MutationScore, string ReportPath, string JsonReportPath, string Tool, string Command);

public sealed record TestActionResult(int Iteration, ExecutionStepStatus Status, IReadOnlyList<string> GeneratedFiles);

public sealed record TestExecutionResult(ExecutionStepStatus Status, int Total, int Passed, int Failed, string ReportPath);

public sealed record CommitExecutionResult(string CommitSha, string Branch, int PullRequestId, string Status);

public sealed record FinalReportFileResult(string FinalReportPath, string FinalHtmlReportPath);

public sealed record PipelineNotificationResult(string NotificationStatus);
