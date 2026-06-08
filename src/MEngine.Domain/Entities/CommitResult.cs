using MEngine.Domain.Common;

namespace MEngine.Domain.Entities;

public sealed class CommitResult : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public string CommitSha { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public int PullRequestId { get; set; }
    public string Status { get; set; } = string.Empty;

    public ExecutionRun? ExecutionRun { get; set; }
}
