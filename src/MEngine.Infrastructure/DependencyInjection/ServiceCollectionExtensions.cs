using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MEngine.Application.Abstractions.Persistence;
using MEngine.Application.Abstractions.Services;
using MEngine.Infrastructure.Persistence;
using MEngine.Infrastructure.Persistence.Repositories;
using MEngine.Infrastructure.Services;

namespace MEngine.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MEngineDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("MEngineDb")
                ?? "Data Source=m-engine.db";
            options.UseSqlite(connectionString);
        });

        services.AddScoped<IAgentConfigurationRepository, AgentConfigurationRepository>();
        services.AddScoped<IExecutionRunRepository, ExecutionRunRepository>();
        services.AddScoped<IExecutionStepRepository, ExecutionStepRepository>();
        services.AddScoped<IRepositoryAnalysisRepository, RepositoryAnalysisRepository>();
        services.AddScoped<IMutationReportRepository, MutationReportRepository>();
        services.AddScoped<ITestDecisionRepository, TestDecisionRepository>();
        services.AddScoped<ITestRunRepository, TestRunRepository>();
        services.AddScoped<ICommitResultRepository, CommitResultRepository>();
        services.AddScoped<IFinalReportRepository, FinalReportRepository>();
        services.AddScoped<IPipelineNotificationRepository, PipelineNotificationRepository>();

        services.AddScoped<IGitService, GitService>();
        services.AddScoped<IAgentProfilingService, AgentProfilingService>();
        services.AddScoped<IMutationTestingService, StrykerMutationTestingService>();
        services.AddScoped<ITestGenerationService, TestGenerationService>();
        services.AddScoped<ITestExecutionService, TestExecutionService>();
        services.AddScoped<ICommitService, CommitService>();
        services.AddScoped<IArtifactFileService, ArtifactFileService>();
        services.AddScoped<IPipelineNotifier, PipelineNotifier>();

        return services;
    }
}
