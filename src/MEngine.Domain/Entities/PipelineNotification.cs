using MEngine.Domain.Common;

namespace MEngine.Domain.Entities;

public sealed class PipelineNotification : AuditableEntity
{
    public Guid ExecutionRunId { get; set; }
    public string NotificationStatus { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;

    public ExecutionRun? ExecutionRun { get; set; }
}
