using AiDev.Executors;
using Microsoft.Extensions.DependencyInjection;

namespace AiDev.Extensions;

public static class OllamaExecutorExtensions
{
    /// <summary>
    /// Registers the Ollama HTTP executor and its named HttpClients.
    /// Call this from the host application's service registration alongside AddAiDevCore().
    /// </summary>
    public static IServiceCollection AddOllamaExecutor(this IServiceCollection services)
    {
        // The inference client needs a long timeout; the health probe should fail fast.
        services.AddHttpClient("ollama", c => c.Timeout = TimeSpan.FromMinutes(10));
        services.AddHttpClient("ollama-health", c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<IAgentExecutor, OllamaAgentExecutor>();
        return services;
    }
}
