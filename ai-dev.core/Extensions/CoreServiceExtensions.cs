using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;
using AiDev.Features.Digest;
using AiDev.Features.Journal;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Features.Workspace;
using AiDev.Services;

using GitService = AiDev.Features.Git.GitService;

namespace AiDev.Extensions;

public static class CoreServiceExtensions
{
    /// <summary>
    /// Registers all ai-dev.core services. Executor implementations (Claude, Ollama, etc.)
    /// are registered separately via their own extension methods (AddClaudeExecutor, AddOllamaExecutor)
    /// so each executor's dependencies stay in its own project.
    /// </summary>
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
        services.AddSingleton<PlaybookService>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<GitService>();
        services.AddSingleton<PromptEnhancerService>();
        services.AddSingleton<AgentRunnerService>();

        // ExecutorHealthMonitor polls all registered IAgentExecutor implementations.
        // Registered as both a singleton (so it can be injected by name) and a hosted service.
        services.AddSingleton<ExecutorHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<ExecutorHealthMonitor>());

        services.AddHostedService<DispatcherService>();
        services.AddHostedService<OverwatchService>();

        return services;
    }
}
