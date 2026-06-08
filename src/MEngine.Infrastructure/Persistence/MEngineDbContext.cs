using Microsoft.EntityFrameworkCore;
using MEngine.Domain.Entities;

namespace MEngine.Infrastructure.Persistence;

public sealed class MEngineDbContext(DbContextOptions<MEngineDbContext> options) : DbContext(options)
{
    public DbSet<AgentConfiguration> AgentConfigurations => Set<AgentConfiguration>();
    public DbSet<ExecutionRun> ExecutionRuns => Set<ExecutionRun>();
    public DbSet<ExecutionStep> ExecutionSteps => Set<ExecutionStep>();
    public DbSet<RepositoryAnalysis> RepositoryAnalyses => Set<RepositoryAnalysis>();
    public DbSet<MutationReport> MutationReports => Set<MutationReport>();
    public DbSet<TestDecision> TestDecisions => Set<TestDecision>();
    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<CommitResult> CommitResults => Set<CommitResult>();
    public DbSet<FinalReport> FinalReports => Set<FinalReport>();
    public DbSet<PipelineNotification> PipelineNotifications => Set<PipelineNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MEngineDbContext).Assembly);
    }
}
