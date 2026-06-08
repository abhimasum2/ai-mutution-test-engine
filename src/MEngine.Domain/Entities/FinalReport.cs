using MEngine.Domain.Common;

namespace MEngine.Domain.Entities;

public sealed class FinalReport : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public string FinalReportPath { get; set; } = string.Empty;
    public string FinalHtmlReportPath { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    public ExecutionRun? ExecutionRun { get; set; }
}
