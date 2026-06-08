using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IFinalReportRepository
{
    Task<FinalReport?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(FinalReport entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
