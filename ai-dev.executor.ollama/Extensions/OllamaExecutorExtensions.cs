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
        // The inference client needs a long timeout. The global Aspire standard resilience
        // handler (10s AttemptTimeout + retries) is unsuitable for LLM inference — a large
        // model like gemma3:27b can take 30+ seconds before the first token. Remove it and
        // rely solely on HttpClient.Timeout as the overall guard.
#pragma warning disable EXTEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
        services.AddHttpClient("ollama", c => c.Timeout = TimeSpan.FromMinutes(10))
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        services.AddHttpClient("ollama-health", c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<IAgentExecutor, OllamaAgentExecutor>();
        return services;
    }
}
