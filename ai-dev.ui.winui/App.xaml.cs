using AiDev.Core.Local.Extensions;
using AiDev.Features.Workspace;
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
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("appsettings.json", optional: true);
            builder.Configuration.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, FilePathConstants.StudioSettingsFileName),
                optional: true, reloadOnChange: false);
            builder.AddServiceDefaults();
            ConfigureServices(builder.Configuration, builder.Services);
            _host = builder.Build();

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
        // Activate the last-used project from the global registry (if any) before DI is built,
        // so that the lazy WorkspacePaths singleton resolves correctly on first access.
        var holder = new ActiveWorkspaceHolder();
        var lastActive = configuration["ActiveProjectPath"]
            ?? ReadLastActiveFromRegistry();
        if (!string.IsNullOrWhiteSpace(lastActive) && Directory.Exists(lastActive))
            holder.Activate(lastActive);

        services.AddSingleton(holder);
        services.AddSingleton<WorkspacePaths>(sp => sp.GetRequiredService<ActiveWorkspaceHolder>().Paths);

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

    private static string? ReadLastActiveFromRegistry()
    {
        try
        {
            if (!File.Exists(GlobalPaths.RegistryFile)) return null;
            var json = File.ReadAllText(GlobalPaths.RegistryFile);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("lastActivePath", out var prop))
                return prop.GetString();
        }
        catch { }
        return null;
    }
}
