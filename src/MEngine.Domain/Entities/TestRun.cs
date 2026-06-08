using MEngine.Domain.Common;
using MEngine.Domain.Enums;

namespace MEngine.Domain.Entities;

public sealed class TestRun : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public int Iteration { get; set; }
    public ExecutionStepStatus Status { get; set; }
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public string ReportPath { get; set; } = string.Empty;

    public ExecutionRun? ExecutionRun { get; set; }
}
