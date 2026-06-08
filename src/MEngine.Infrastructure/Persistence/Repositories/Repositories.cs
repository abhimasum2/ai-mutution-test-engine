using Microsoft.EntityFrameworkCore;
using MEngine.Application.Abstractions.Persistence;
using MEngine.Domain.Entities;

namespace MEngine.Infrastructure.Persistence.Repositories;

public sealed class AgentConfigurationRepository(MEngineDbContext dbContext) : IAgentConfigurationRepository
{
    public Task<AgentConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.AgentConfigurations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<AgentConfiguration?> GetByNameAsync(string agentName, CancellationToken cancellationToken = default)
        => dbContext.AgentConfigurations.FirstOrDefaultAsync(x => x.AgentName == agentName, cancellationToken);

    public Task AddAsync(AgentConfiguration entity, CancellationToken cancellationToken = default)
        => dbContext.AgentConfigurations.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ExecutionRunRepository(MEngineDbContext dbContext) : IExecutionRunRepository
{
    public Task<ExecutionRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.ExecutionRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddAsync(ExecutionRun entity, CancellationToken cancellationToken = default)
        => dbContext.ExecutionRuns.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ExecutionStepRepository(MEngineDbContext dbContext) : IExecutionStepRepository
{
    public Task AddAsync(ExecutionStep entity, CancellationToken cancellationToken = default)
        => dbContext.ExecutionSteps.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class RepositoryAnalysisRepository(MEngineDbContext dbContext) : IRepositoryAnalysisRepository
{
    public Task<RepositoryAnalysis?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.RepositoryAnalyses
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(RepositoryAnalysis entity, CancellationToken cancellationToken = default)
        => dbContext.RepositoryAnalyses.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class MutationReportRepository(MEngineDbContext dbContext) : IMutationReportRepository
{
    public Task<MutationReport?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.MutationReports
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(MutationReport entity, CancellationToken cancellationToken = default)
        => dbContext.MutationReports.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class TestDecisionRepository(MEngineDbContext dbContext) : ITestDecisionRepository
{
    public Task<TestDecision?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.TestDecisions
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(TestDecision entity, CancellationToken cancellationToken = default)
        => dbContext.TestDecisions.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class TestRunRepository(MEngineDbContext dbContext) : ITestRunRepository
{
    public Task<TestRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.TestRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<TestRun?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.TestRuns
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(TestRun entity, CancellationToken cancellationToken = default)
        => dbContext.TestRuns.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class CommitResultRepository(MEngineDbContext dbContext) : ICommitResultRepository
{
    public Task<CommitResult?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.CommitResults
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(CommitResult entity, CancellationToken cancellationToken = default)
        => dbContext.CommitResults.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class FinalReportRepository(MEngineDbContext dbContext) : IFinalReportRepository
{
    public Task<FinalReport?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.FinalReports
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(FinalReport entity, CancellationToken cancellationToken = default)
        => dbContext.FinalReports.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class PipelineNotificationRepository(MEngineDbContext dbContext) : IPipelineNotificationRepository
{
    public Task<PipelineNotification?> GetLatestByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        => dbContext.PipelineNotifications
            .Where(x => x.ExecutionRunId == runId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(PipelineNotification entity, CancellationToken cancellationToken = default)
        => dbContext.PipelineNotifications.AddAsync(entity, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
