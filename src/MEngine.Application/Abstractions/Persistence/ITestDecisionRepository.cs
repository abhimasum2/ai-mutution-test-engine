using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface ITestDecisionRepository
{
    Task<TestDecision?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(TestDecision entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
