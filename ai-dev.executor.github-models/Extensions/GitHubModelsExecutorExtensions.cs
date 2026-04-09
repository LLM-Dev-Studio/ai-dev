using AiDev.Executors;
using Microsoft.Extensions.DependencyInjection;

namespace AiDev.Extensions;

public static class GitHubModelsExecutorExtensions
{
    /// <summary>
    /// Registers the GitHub Models API executor. Call this from the host application's
    /// service registration alongside AddAiDevCore().
    ///
    /// The executor reads GitHubToken from StudioSettings (studio-settings.json).
    /// Requires Microsoft.Extensions.Http to be available in the host project.
    /// </summary>
    public static IServiceCollection AddGitHubModelsExecutor(this IServiceCollection services)
    {
        services.AddHttpClient("github-models", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddHttpClient("github-models-health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddSingleton<IAgentExecutor, GitHubModelsAgentExecutor>();
        return services;
    }
}
