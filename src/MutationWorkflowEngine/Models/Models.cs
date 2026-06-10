namespace MutationWorkflowEngine.Models;

internal enum TestingFramework
{
    Unknown,
    NUnit,
    XUnit,
    MSTest
}

internal sealed record AppConfig(
    string RepositoryRoot,
    string TargetProjectPath,
    string? TestProjectPath,
    string ReportsDirectory,
    string BaseRef,
    string OpenAiBaseUrl,
    string OpenAiModel,
    string OpenAiApiKey,
    bool CommitAndPush,
    int GenerationMaxIterations,
    int MaxSourceFileChars,
    int MaxConcurrency,
    int ProcessTimeoutMinutes,
    bool Verbose);

internal sealed record AppConfigFile(
    string? RepositoryRoot,
    string? TargetProjectPath,
    string? TestProjectPath,
    string? ReportsDirectory,
    string? BaseRef,
    string? OpenAiBaseUrl,
    string? OpenAiModel,
    string? OpenAiApiKey,
    bool? CommitAndPush,
    int? GenerationMaxIterations,
    int? MaxSourceFileChars,
    int? MaxConcurrency,
    int? ProcessTimeoutMinutes,
    bool? Verbose);

internal sealed record WorkflowContext(
    string RepositoryRoot,
    string TargetProjectPath,
    string TestProjectPath,
    TestingFramework TestingFramework,
    IReadOnlyList<string> ChangedFiles,
    string PreCommitReportPath,
    string PostCommitReportPath,
    IReadOnlyList<string> UpdatedTestFiles);

internal sealed record MutationFileResult(
    string SourceFile,
    int Killed,
    int Survived,
    int Total,
    double Score);

internal sealed record MutationReportSummary(
    string ReportPath,
    int TotalKilled,
    int TotalSurvived,
    int TotalMutants,
    double Score,
    IReadOnlyList<MutationFileResult> Files);

internal sealed record GeneratedTestPatch(
    string RelativeTestFilePath,
    string Content,
    string Reasoning);

internal sealed record TokenUsageRecord(
    string SourceFile,
    int InputTokens,
    int OutputTokens,
    int TotalTokens);

internal sealed record TokenUsageReport(
    int TotalInputTokens,
    int TotalOutputTokens,
    int TotalTokens,
    IReadOnlyList<TokenUsageRecord> Records);

internal sealed record PerformanceStageTiming(string Stage, TimeSpan Duration);

internal sealed record PerformanceReport(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    TimeSpan TotalDuration,
    IReadOnlyList<PerformanceStageTiming> Stages);
