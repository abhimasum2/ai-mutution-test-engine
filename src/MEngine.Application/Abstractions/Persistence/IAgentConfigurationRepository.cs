using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IAgentConfigurationRepository
{
    Task<AgentConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AgentConfiguration?> GetByNameAsync(string agentName, CancellationToken cancellationToken = default);
    Task AddAsync(AgentConfiguration entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
