using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Dispatching;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

/// <summary>
/// Represents executor health status for UI display.
/// </summary>
public partial class ExecutorStatusItem : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string HealthColor { get; set; } = "#6B7280";
}

/// <summary>
/// Provides shared application shell state for the main UI.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ExecutorHealthMonitor _healthMonitor;
    private readonly MessagesService _messagesService;
    private readonly IDecisionsService _decisionsService;
    private IDisposable? _healthSubscription;

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
    [ObservableProperty] public partial DecisionId? PendingDecisionId { get; set; }

    public ObservableCollection<ExecutorStatusItem> ExecutorStatuses { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="healthMonitor">The executor health monitor.</param>
    /// <param name="messagesService">The messages service.</param>
    /// <param name="decisionsService">The decisions service.</param>
    public MainViewModel(ExecutorHealthMonitor healthMonitor, MessagesService messagesService, IDecisionsService decisionsService)
    {
        _healthMonitor = healthMonitor;
        _messagesService = messagesService;
        _decisionsService = decisionsService;
    }

    /// <summary>
    /// Sets the active project for the shell.
    /// </summary>
    /// <param name="project">The active project.</param>
    public void SetActiveProject(ProjectDetail? project)
    {
        ActiveProject = project;
    }

    /// <summary>
    /// Refreshes navigation badge counts for the active project.
    /// </summary>
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
            var decisions = _decisionsService.ListDecisions(ActiveProject.Slug, DecisionStatus.Pending);
            PendingDecisionCount = decisions.Count;
        }
        catch
        {
            PendingDecisionCount = 0;
        }
    }

    /// <summary>
    /// Loads executor health status items for display.
    /// </summary>
    /// <returns>A task that completes when executor statuses are loaded.</returns>
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

    /// <summary>
    /// Starts listening for live executor health updates.
    /// </summary>
    /// <param name="dispatcher">The dispatcher used to marshal updates to the UI thread.</param>
    public void StartLiveHealthUpdates(DispatcherQueue dispatcher)
    {
        _healthSubscription?.Dispose();
        _healthSubscription = _healthMonitor.SubscribeChanged(() =>
            dispatcher.TryEnqueue(async () => await LoadExecutorStatusesAsync()));
    }

    /// <summary>
    /// Stops listening for live executor health updates.
    /// </summary>
    public void StopLiveHealthUpdates()
    {
        _healthSubscription?.Dispose();
        _healthSubscription = null;
    }
}
