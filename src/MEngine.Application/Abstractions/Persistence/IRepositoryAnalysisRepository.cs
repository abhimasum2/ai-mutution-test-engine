using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IRepositoryAnalysisRepository
{
    Task<RepositoryAnalysis?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(RepositoryAnalysis entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
