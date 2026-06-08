using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IExecutionRunRepository
{
    Task<ExecutionRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ExecutionRun entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
