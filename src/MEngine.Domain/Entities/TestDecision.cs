using MEngine.Domain.Common;
using MEngine.Domain.Enums;

namespace MEngine.Domain.Entities;

public sealed class TestDecision : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public TestDecisionType Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string TargetFilesJson { get; set; } = "[]";

    public ExecutionRun? ExecutionRun { get; set; }
}
