using MEngine.Domain.Common;
using MEngine.Domain.Enums;

namespace MEngine.Domain.Entities;

public sealed class ExecutionStep : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public ExecutionStepStatus Status { get; set; } = ExecutionStepStatus.Pending;
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public ExecutionRun? ExecutionRun { get; set; }
}
