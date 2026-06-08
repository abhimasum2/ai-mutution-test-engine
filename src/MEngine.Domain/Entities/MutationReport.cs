using MEngine.Domain.Common;

namespace MEngine.Domain.Entities;

public sealed class MutationReport : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public decimal MutationScore { get; set; }
    public string ReportPath { get; set; } = string.Empty;
    public string JsonReportPath { get; set; } = string.Empty;
    public string Tool { get; set; } = "Stryker.NET";
    public string ThresholdsJson { get; set; } = "{}";

    public ExecutionRun? ExecutionRun { get; set; }
}
