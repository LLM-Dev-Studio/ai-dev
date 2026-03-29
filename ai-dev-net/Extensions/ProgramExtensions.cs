namespace AiDevNet.Extensions;

public static class ProgramExtensions
{
    internal static IServiceCollection AddProgramDependencies(this IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddSingleton<WorkspacePaths>();
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<StudioSettingsService>();
        services.AddSingleton<AgentTemplatesService>();
        services.AddSingleton<AgentService>();
        services.AddSingleton<BoardService>((sp) =>
            new(
                sp.GetRequiredService<WorkspacePaths>(),
                sp.GetRequiredService<AgentRunnerService>(),
                sp.GetRequiredService<ILogger<BoardService>>()));
        services.AddSingleton<MessageChangedNotifier>();
        services.AddSingleton<MessagesService>();
        services.AddSingleton<DecisionsService>();
        services.AddSingleton<JournalsService>();
        services.AddSingleton<KbService>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<GitService>();
        services.AddHttpClient("ollama");
        services.AddSingleton<IAgentExecutor, ClaudeAgentExecutor>();
        services.AddSingleton<IAgentExecutor, OllamaAgentExecutor>();
        services.AddSingleton<AgentRunnerService>();

        services.AddHostedService<DispatcherService>();
        services.AddHostedService<OverwatchService>();

        return services;
    }
}
