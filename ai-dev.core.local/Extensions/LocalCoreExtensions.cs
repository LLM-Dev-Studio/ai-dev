using AiDev.Core.Local.Implementation;
using AiDev.Core.Local.Implementation.Null;
using AiDev.Core.Local.Orchestration;
using AiDev.Features.Agent;


namespace AiDev.Core.Local.Extensions;

public static class LocalCoreExtensions
{
    public static IServiceCollection AddLocalCore(
        this IServiceCollection services,
        LocalOrchestratorOptions? options = null)
    {
        services.AddSingleton(options ?? LocalOrchestratorOptions.Default);

        services.AddSingleton<ILocalPlanner, NullPlanner>();
        services.AddSingleton<ILocalToolBroker, NullToolBroker>();
        services.AddSingleton<IProgressiveDiscoveryEngine, NullDiscoveryEngine>();
        services.AddSingleton<IContextCompactor, NullCompactor>();
        services.AddSingleton<IModelStrategyResolver, StaticModelStrategyResolver>();
        services.AddSingleton<IRuntimeMemoryStore, InMemoryRuntimeMemoryStore>();
        services.AddSingleton<ILocalOrchestrator, LocalOrchestrator>();
        services.AddSingleton<ILocalAgentHook, LocalAgentHookAdapter>();

        return services;
    }
}
