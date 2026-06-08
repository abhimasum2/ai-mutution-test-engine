using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface ITestRunRepository
{
    Task<TestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TestRun?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(TestRun entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
