using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class ExecutorStatusItem : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string HealthColor { get; set; } = "#6B7280";
}

public partial class MainViewModel : ObservableObject
{
    private readonly ExecutorHealthMonitor _healthMonitor;
    private readonly MessagesService _messagesService;
    private readonly DecisionsService _decisionsService;

    [ObservableProperty] public partial ProjectDetail? ActiveProject { get; set; }
    [ObservableProperty] public partial AgentInfo? PendingAgent { get; set; }
    [ObservableProperty] public partial int UnreadMessageCount { get; set; }
    [ObservableProperty] public partial int PendingDecisionCount { get; set; }

    private TaskId? _pendingTaskId;
    public TaskId? PendingTaskId
    {
        get => _pendingTaskId;
        set => SetProperty(ref _pendingTaskId, value);
    }
    [ObservableProperty] public partial string? PendingDecisionId { get; set; }

    public ObservableCollection<ExecutorStatusItem> ExecutorStatuses { get; } = [];

    public MainViewModel(ExecutorHealthMonitor healthMonitor, MessagesService messagesService, DecisionsService decisionsService)
    {
        _healthMonitor = healthMonitor;
        _messagesService = messagesService;
        _decisionsService = decisionsService;
    }

    public void SetActiveProject(ProjectDetail? project)
    {
        ActiveProject = project;
    }

    public void RefreshNavBadges()
    {
        if (ActiveProject is null)
        {
            UnreadMessageCount = 0;
            PendingDecisionCount = 0;
            return;
        }
        try
        {
            var messages = _messagesService.ListMessages(ActiveProject.Slug);
            UnreadMessageCount = messages.Count(m => !m.IsProcessed);
        }
        catch
        {
            UnreadMessageCount = 0;
        }
        try
        {
            var decisions = _decisionsService.ListDecisions(ActiveProject.Slug, "pending");
            PendingDecisionCount = decisions.Count;
        }
        catch
        {
            PendingDecisionCount = 0;
        }
    }

    public async Task LoadExecutorStatusesAsync()
    {
        try
        {
            var results = await Task.Run(() => _healthMonitor.GetExecutorHealth());
            ExecutorStatuses.Clear();
            foreach (var (executor, health) in results)
            {
                ExecutorStatuses.Add(new ExecutorStatusItem
                {
                    Name = executor.Name,
                    HealthColor = health.IsHealthy ? "#22C55E" : "#EF4444"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Executor health check failed at startup: {ex.Message}");
        }
    }
}
