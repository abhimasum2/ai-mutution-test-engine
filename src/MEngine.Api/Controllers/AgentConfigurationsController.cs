using Microsoft.AspNetCore.Mvc;
using MEngine.Application.Abstractions.Services;
using MEngine.Application.DTOs.AgentConfigurations;

namespace MEngine.Api.Controllers;

[ApiController]
[Route("api/agent-configurations")]
public sealed class AgentConfigurationsController(IOrchestrationService orchestrationService) : ControllerBase
{
    /// <summary>
    /// Validates an agent configuration and persists the validation outcome.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateAgentConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateAgentConfigurationResponse>> ValidateAsync(
        [FromBody] ValidateAgentConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await orchestrationService.ValidateAgentConfigurationAsync(request, GetCorrelationId(), cancellationToken);
        return Ok(response);
    }

    private string GetCorrelationId()
        => Request.Headers.TryGetValue("X-Correlation-ID", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : HttpContext.TraceIdentifier;
}
