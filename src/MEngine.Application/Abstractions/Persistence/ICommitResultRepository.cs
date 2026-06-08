using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface ICommitResultRepository
{
    Task<CommitResult?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(CommitResult entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
