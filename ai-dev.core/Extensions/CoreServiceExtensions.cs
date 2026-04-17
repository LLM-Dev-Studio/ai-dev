using AiDev.Executors;
using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;
using AiDev.Features.Digest;
using AiDev.Features.Insights;
using AiDev.Features.Journal;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Features.Secrets;
using AiDev.Features.Workspace;

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
        services.AddSingleton<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
        services.AddSingleton<IDomainEventHandler<TaskAssigned>, TaskAssignedHandler>();
        services.AddSingleton<IDomainEventHandler<DecisionResolved>, DecisionResolvedHandler>();
        services.AddSingleton<AtomicFileWriter>();
        services.AddSingleton<ProjectMutationCoordinator>();
        services.AddSingleton<ConsistencyCheckService>();
        services.AddSingleton<SecretsService>();
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<StudioSettingsService>();
        services.AddSingleton<AgentTemplatesService>();
        services.AddSingleton<AgentService>();
        services.AddSingleton<BoardService>();
        services.AddSingleton<MessageChangedNotifier>();
        services.AddSingleton<DecisionChangedNotifier>();
        services.AddSingleton<MessagesService>();
        services.AddSingleton<DecisionsService>();
        services.AddSingleton<DecisionChatService>();
        services.AddSingleton<JournalsService>();
        services.AddSingleton<KbService>();
        services.AddSingleton<PlaybookService>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<GitService>();
        services.AddSingleton<PromptEnhancerService>();
        services.AddSingleton<InsightsService>();
        services.AddSingleton<AgentRunnerService>();

        // ExecutorHealthMonitor polls all registered IAgentExecutor implementations.
        // Registered as both a singleton (so it can be injected by name) and a hosted service.
        services.AddSingleton<ExecutorHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<ExecutorHealthMonitor>());

        // ModelRegistry aggregates KnownModels + health-discovered models from all executors.
        services.AddSingleton<IModelRegistry, ModelRegistry>();

        services.AddHostedService<ConsistencyCheckHostedService>();
        services.AddHostedService<DispatcherService>();
        services.AddHostedService<OverwatchService>();

        return services;
    }
}
