using System.ComponentModel.DataAnnotations;

namespace MEngine.Application.DTOs.AgentConfigurations;

public sealed class ValidateAgentConfigurationRequest
{
    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    [Url]
    [MaxLength(500)]
    public string EndpointUrl { get; set; } = string.Empty;
}

public sealed record ValidateAgentConfigurationResponse(string Status);
