using AiDev.Executors;
using Microsoft.Extensions.DependencyInjection;

namespace AiDev.Extensions;

public static class CopilotCliExecutorExtensions
{
    /// <summary>
    /// Registers the GitHub Copilot CLI executor. Call this from the host application's
    /// service registration alongside AddAiDevCore().
    ///
    /// The executor shells out to the installed <c>copilot</c> CLI and drives it in
    /// non-interactive mode (<c>-p &lt;prompt&gt; --output-format json --allow-all-tools</c>),
    /// streaming its JSONL events back into the agent transcript channel.
    /// </summary>
    public static IServiceCollection AddCopilotCliExecutor(this IServiceCollection services)
    {
        services.AddSingleton<IAgentExecutor, CopilotCliAgentExecutor>();
        return services;
    }
}
