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

        services.AddSingleton<ILlmClient, OllamaLlmClient>();
        services.AddSingleton<ILocalPlanner, RoleBasedLlmPlanner>();
        services.AddSingleton<ILocalToolBroker, LocalToolBroker>();
        services.AddSingleton<IProgressiveDiscoveryEngine, ProgressiveDiscoveryEngine>();
        services.AddSingleton<IContextCompactor, RuleBasedContextCompactor>();
        services.AddSingleton<IModelStrategyResolver, StaticModelStrategyResolver>();
        services.AddSingleton<IRuntimeMemoryStore, FileSystemRuntimeMemoryStore>();
        services.AddSingleton<ILocalOrchestrator, LocalOrchestrator>();
        services.AddSingleton<ILocalAgentHook, LocalAgentHookAdapter>();

        return services;
    }
}
