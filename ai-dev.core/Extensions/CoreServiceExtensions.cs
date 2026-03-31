using AiDev.Executors;
using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;
using AiDev.Features.Digest;
using AiDev.Features.Journal;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Workspace;
using AiDev.Services;

using GitService = AiDev.Features.Git.GitService;

namespace AiDev.Extensions;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddAiDevCore(this IServiceCollection services)
    {
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
        services.AddSingleton<DecisionChangedNotifier>();
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
