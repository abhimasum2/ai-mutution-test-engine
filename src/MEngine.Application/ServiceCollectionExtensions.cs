using Microsoft.Extensions.DependencyInjection;
using MEngine.Application.Abstractions.Services;
using MEngine.Application.Services;

namespace MEngine.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrchestrationService, OrchestrationService>();
        return services;
    }
}
