using AiDev.Desktop.ViewModels;
using AiDev.Desktop.Views.Pages;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.IO;
using System.Windows;

using Wpf.Ui;

namespace AiDev.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((ctx, services) => ConfigureServices(ctx.Configuration, services))
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // Resolve workspace root: config > auto-discover near executable
        var configuredPath = configuration["WorkspacesPath"];
        var workspaceRoot = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : DiscoverWorkspacesPath();

        services.AddSingleton<WorkspacePaths>(new WorkspacePaths(new RootDir(workspaceRoot)));

        // Core domain services
        services.AddAiDevCore();

        // Executor plugins
        services.AddClaudeExecutor();
        services.AddAnthropicExecutor();
        services.AddOllamaExecutor();
        services.AddGitHubModelsExecutor();
        services.AddLmStudioExecutor();

        // WPF UI services
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();

        // Windows and pages
        services.AddSingleton<MainWindow>();
        services.AddTransient<ProjectsPage>();
        services.AddTransient<AgentDashboardPage>();
        services.AddTransient<BoardPage>();
        services.AddTransient<MessagesPage>();
        services.AddTransient<DecisionsPage>();
        services.AddTransient<JournalsPage>();
        services.AddTransient<KnowledgeBasePage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<AgentDetailPage>();
        services.AddTransient<TranscriptPage>();
        services.AddTransient<SecretsPage>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<AgentDashboardViewModel>();
        services.AddTransient<BoardViewModel>();
        services.AddTransient<MessagesViewModel>();
        services.AddTransient<DecisionsViewModel>();
        services.AddTransient<JournalsViewModel>();
        services.AddTransient<KnowledgeBaseViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AgentDetailViewModel>();
        services.AddTransient<TranscriptViewModel>();
        services.AddTransient<SecretsViewModel>();
    }

    /// <summary>
    /// Walk up from the executable directory looking for a <c>workspaces</c> folder,
    /// so the app works out of the box in the dev repo layout.
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

        // Fallback: workspaces alongside the executable
        return Path.Combine(AppContext.BaseDirectory, "workspaces");
    }
}
