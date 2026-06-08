using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IMutationReportRepository
{
    Task<MutationReport?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(MutationReport entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
