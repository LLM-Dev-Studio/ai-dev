using AiDev.Executors;
using Microsoft.Extensions.DependencyInjection;

namespace AiDev.Extensions;

public static class LmStudioExecutorExtensions
{
    /// <summary>
    /// Registers the LM Studio HTTP executor and its named HttpClients.
    /// Call this from the host application's service registration alongside AddAiDevCore().
    /// </summary>
    public static IServiceCollection AddLmStudioExecutor(this IServiceCollection services)
    {
        // The inference client needs a long timeout — local models can take 30+ seconds
        // before the first token. Remove the Aspire standard resilience handler and rely
        // solely on HttpClient.Timeout as the overall guard.
#pragma warning disable EXTEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
        services.AddHttpClient("lmstudio", c => c.Timeout = TimeSpan.FromMinutes(10))
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        services.AddHttpClient("lmstudio-health", c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<IAgentExecutor, LmStudioAgentExecutor>();
        return services;
    }
}
