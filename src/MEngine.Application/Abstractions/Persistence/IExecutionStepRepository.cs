using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IExecutionStepRepository
{
    Task AddAsync(ExecutionStep entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
