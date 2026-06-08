using System.ComponentModel.DataAnnotations;
using MEngine.Domain.Enums;

namespace MEngine.Application.DTOs.Runs;

public sealed class CreateRunRequest
{
    [Required]
    [Url]
    [MaxLength(500)]
    public string RepositoryUrl { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PullRequestId { get; set; }

    [Required]
    public Guid AgentConfigurationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, 100)]
    public int MaxIterations { get; set; } = 3;

    [Required]
    [MaxLength(500)]
    public string OutputFolder { get; set; } = string.Empty;

    public bool NotifyPipeline { get; set; }
}

public sealed record CreateRunResponse(Guid RunId, string Status);

public sealed record RunStatusResponse(Guid RunId, string Status, int CurrentIteration, DateTimeOffset UpdatedAtUtc);

public sealed record ProfileRunResponse(string Language, string TestFramework, string ProfileSummary, bool MasterPromptApplied);

public sealed record RepositoryAnalysisResponse(string BuildStatus, IReadOnlyList<string> ChangedFiles, string RepoSummary);

public sealed class GenerateMutationReportRequest
{
    [Required]
    [MaxLength(500)]
    public string TestProjectPath { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string SolutionPath { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> Reporters { get; set; } = new() { "html", "json" };

    [Required]
    public Dictionary<string, int> Thresholds { get; set; } = new();
}

public sealed record MutationReportResponse(decimal MutationScore, string ReportPath, string JsonReportPath, string Tool);

public sealed record TestDecisionResponse(TestDecisionType Decision, string Reason, IReadOnlyList<string> TargetFiles);

public sealed class TestActionRequest
{
    [Required]
    public TestDecisionType Decision { get; set; }

    [Range(1, 100)]
    public int MaxIterations { get; set; } = 3;

    [MaxLength(4000)]
    public string? AgentPromptOverride { get; set; }

    [Required]
    [MinLength(1)]
    public List<string> TargetProjects { get; set; } = new();
}

public sealed record TestActionResponse(int Iteration, string Status, IReadOnlyList<string> GeneratedFiles);

public sealed record TestRunResponse(Guid TestRunId, string Status, int Total, int Passed, int Failed, string ReportPath);

public sealed record CommitResponse(string CommitSha, string Branch, int PullRequestId);

public sealed record FinalReportResponse(string FinalReportPath, string FinalHtmlReportPath);

public sealed record PipelineNotificationResponse(string NotificationStatus);
