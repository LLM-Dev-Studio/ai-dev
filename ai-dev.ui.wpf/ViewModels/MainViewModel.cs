using AiDev.Features.Workspace;
using AiDev.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class ExecutorStatusItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _healthColor = "#6B7280";
}

public partial class MainViewModel : ObservableObject
{
    private readonly ExecutorHealthMonitor _healthMonitor;

    [ObservableProperty] private ProjectDetail? _activeProject;
    [ObservableProperty] private string _windowTitle = "AI Dev Net";
    [ObservableProperty] private AgentInfo? _pendingAgent;

    public ObservableCollection<ExecutorStatusItem> ExecutorStatuses { get; } = [];

    public MainViewModel(ExecutorHealthMonitor healthMonitor)
    {
        _healthMonitor = healthMonitor;
    }

    public void SetActiveProject(ProjectDetail? project)
    {
        ActiveProject = project;
        WindowTitle = project is not null
            ? $"AI Dev Net — {project.Name}"
            : "AI Dev Net";
    }

    public async Task LoadExecutorStatusesAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var results = _healthMonitor.GetExecutorHealth();
                App.Current.Dispatcher.Invoke(() =>
                {
                    ExecutorStatuses.Clear();
                    foreach (var (executor, health) in results)
                    {
                        ExecutorStatuses.Add(new ExecutorStatusItem
                        {
                            Name = executor.Name,
                            HealthColor = health.IsHealthy ? "#22C55E" : "#EF4444"
                        });
                    }
                });
            });
        }
        catch { /* Executors may not be available */ }
    }
}
