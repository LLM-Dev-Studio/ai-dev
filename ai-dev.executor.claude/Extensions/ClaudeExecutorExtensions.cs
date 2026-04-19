using AiDev.Executors;
using AiDev.Features.Planning;
using Microsoft.Extensions.DependencyInjection;

namespace AiDev.Extensions;

public static class ClaudeExecutorExtensions
{
    /// <summary>
    /// Registers the Claude CLI executor. Call this from the host application's
    /// service registration alongside AddAiDevCore().
    /// </summary>
    public static IServiceCollection AddClaudeExecutor(this IServiceCollection services)
    {
        services.AddSingleton<ClaudeAgentExecutor>();
        services.AddSingleton<IAgentExecutor>(sp => sp.GetRequiredService<ClaudeAgentExecutor>());
        services.AddSingleton<IPlanningLlmClient, ClaudePlanningLlmClient>();
        return services;
    }
}
