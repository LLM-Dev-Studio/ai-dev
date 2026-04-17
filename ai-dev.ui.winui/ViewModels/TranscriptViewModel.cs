using AiDev.Executors;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class TranscriptViewModel : ObservableObject
{
    private readonly AgentService _agentService;
    private readonly AgentRunnerService _agentRunnerService;
    private readonly IModelRegistry _modelRegistry;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial AgentInfo? Agent { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial TranscriptDate? SelectedDate { get; set; }

    [ObservableProperty]
    public partial string TranscriptContent { get; set; } = "";

    /// <summary>Formatted token + cost summary for the selected session, e.g. "in: 12,345  out: 4,567  ~$0.0234"</summary>
    [ObservableProperty]
    public partial string TokenSummary { get; set; } = "";

    public ObservableCollection<TranscriptDate> Dates { get; } = [];

    public event Action? NavigateBack;

    public TranscriptViewModel(
        AgentService agentService,
        AgentRunnerService agentRunnerService,
        IModelRegistry modelRegistry,
        MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _agentRunnerService = agentRunnerService;
        _modelRegistry = modelRegistry;
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private void GoBack() => NavigateBack?.Invoke();

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    public async Task LoadAsync(AgentSlug agentSlug)
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            Agent = _agentService.LoadAgent(CurrentSlug, agentSlug);

            var dates = _agentService.ListTranscriptDates(CurrentSlug, agentSlug);
            Dates.Clear();
            foreach (var d in dates.OrderByDescending(d => d.Value))
                Dates.Add(d);

            if (Dates.Count > 0)
                await SelectDateAsync(Dates[0]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SelectDateAsync(TranscriptDate date)
    {
        if (CurrentSlug is null || Agent is null) return;
        SelectedDate = date;
        TranscriptContent = _agentService.ReadTranscript(CurrentSlug, Agent.Slug, date);

        var usage = _agentRunnerService.GetSessionUsage(CurrentSlug, Agent.Slug, date);
        if (usage is null)
        {
            TokenSummary = "";
        }
        else
        {
            var model = _modelRegistry.Find(Agent.Executor.Value, Agent.Model);
            var cost = usage.EstimatedCost(model);
            var parts = new System.Text.StringBuilder();
            parts.Append($"in: {usage.InputTokens:N0}  out: {usage.OutputTokens:N0}");
            if (usage.CacheReadTokens > 0)
                parts.Append($"  cache: {usage.CacheReadTokens:N0}");
            if (cost.HasValue)
                parts.Append($"  ~${cost.Value:F4}");
            TokenSummary = parts.ToString();
        }
        await Task.CompletedTask;
    }
}
