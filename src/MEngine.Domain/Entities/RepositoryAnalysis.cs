using MEngine.Domain.Common;

namespace MEngine.Domain.Entities;

public sealed class RepositoryAnalysis : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public string BuildStatus { get; set; } = string.Empty;
    public string ChangedFilesJson { get; set; } = "[]";
    public string RepoSummary { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TestFramework { get; set; } = string.Empty;
    public string ProfileSummary { get; set; } = string.Empty;
    public bool MasterPromptApplied { get; set; }

    public ExecutionRun? ExecutionRun { get; set; }
}
