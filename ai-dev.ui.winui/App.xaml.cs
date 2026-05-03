using AiDev.Core.Local.Extensions;
using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace AiDev.WinUI;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services => ((App)Current)._host!.Services;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                    cfg.AddJsonFile("appsettings.json", optional: true))
                .ConfigureServices((ctx, services) =>
                    ConfigureServices(ctx.Configuration, services))
                .Build();

            await _host.StartAsync();

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fatal: application failed to start. {ex}");
            throw;
        }
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        var configuredPath = configuration["WorkspacesPath"];
        var workspaceRoot = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : DiscoverWorkspacesPath();

        services.AddSingleton<WorkspacePaths>(new WorkspacePaths(new RootDir(workspaceRoot)));

        // Core domain services
        services.AddAiDevCore();
        services.AddLocalCore();

        // Executor plugins
        services.AddClaudeExecutor();
        services.AddAnthropicExecutor();
        services.AddOllamaExecutor();
        services.AddGitHubModelsExecutor();
        services.AddLmStudioExecutor();
        services.AddCopilotCliExecutor();

        // Windows and pages
        services.AddSingleton<MainWindow>();

        services.AddSingleton<IUiDispatcher, DispatcherQueueUiDispatcher>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<AgentDashboardViewModel>();
        services.AddTransient<BoardViewModel>();
        services.AddTransient<MessagesViewModel>();
        services.AddTransient<DecisionsViewModel>();
        services.AddTransient<DecisionDetailViewModel>();
        services.AddTransient<JournalsViewModel>();
        services.AddTransient<KnowledgeBaseViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AgentDetailViewModel>();
        services.AddTransient<TranscriptViewModel>();
        services.AddTransient<TaskTranscriptViewModel>();
        services.AddTransient<SecretsViewModel>();
        services.AddTransient<DigestViewModel>();
        services.AddTransient<InsightsViewModel>();
        services.AddTransient<ConsistencyViewModel>();
        services.AddTransient<CodebaseViewModel>();
        services.AddTransient<ProjectSettingsViewModel>();
        services.AddTransient<TemplatesViewModel>();
        services.AddTransient<PreferencesViewModel>();
        services.AddTransient<PlanningTasksViewModel>();
    }

    /// <summary>
    /// Walk up from the executable directory looking for a <c>workspaces</c> folder.
    /// </summary>
    private static string DiscoverWorkspacesPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "workspaces");
            if (Directory.Exists(candidate))
                return candidate;
        }
        return Path.Combine(AppContext.BaseDirectory, "workspaces");
    }
}
