using AiDev.Core.Local.Orchestration;

namespace AiDev.Core.Local.Extensions;

public static class LocalCoreExtensions
{
    public static IServiceCollection AddLocalCore(
        this IServiceCollection services,
        LocalOrchestratorOptions? options = null)
    {
        services.AddSingleton(options ?? LocalOrchestratorOptions.Default);
        return services;
    }
}
