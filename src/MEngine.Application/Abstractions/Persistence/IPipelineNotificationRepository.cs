using MEngine.Domain.Entities;

namespace MEngine.Application.Abstractions.Persistence;

public interface IPipelineNotificationRepository
{
    Task<PipelineNotification?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddAsync(PipelineNotification entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
