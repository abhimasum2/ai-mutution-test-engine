using MEngine.Domain.Common;

namespace MEngine.Domain.Entities;

public sealed class AgentConfiguration : AuditableEntity
{
    public string AgentName { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public bool IsValid { get; set; }
}
