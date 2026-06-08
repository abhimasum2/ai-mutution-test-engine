using MEngine.Domain.Common;
using MEngine.Domain.Enums;

namespace MEngine.Domain.Entities;

public sealed class ExecutionRun : AuditableEntity
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public int PullRequestId { get; set; }
    public Guid AgentConfigurationId { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public int MaxIterations { get; set; } = 3;
    public string OutputFolder { get; set; } = string.Empty;
    public bool NotifyPipeline { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public int CurrentIteration { get; set; }

    public AgentConfiguration? AgentConfiguration { get; set; }
    public ICollection<ExecutionStep> Steps { get; set; } = new List<ExecutionStep>();
}
