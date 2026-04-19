using AiDev.Executors;
using AiDev.Features.Planning;

using Microsoft.Extensions.DependencyInjection;

namespace AiDev.Extensions;

public static class AnthropicExecutorExtensions
{
    /// <summary>
    /// Registers the Anthropic direct-API executor. Call this from the host application's
    /// service registration alongside AddAiDevCore().
    ///
    /// The executor reads AnthropicApiKey from StudioSettings (studio-settings.json).
    /// Requires Microsoft.Extensions.Http to be available in the host project.
    /// </summary>
    public static IServiceCollection AddAnthropicExecutor(this IServiceCollection services)
    {
        // Two named clients: one for inference (long timeout), one for health probes (short).
        services.AddHttpClient("anthropic", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddHttpClient("anthropic-health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddSingleton<IAgentExecutor, AnthropicAgentExecutor>();
        services.AddSingleton<IPlanningChatService, AnthropicPlanningChatService>();
        return services;
    }
}
