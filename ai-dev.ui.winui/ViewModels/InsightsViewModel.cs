using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.WinUI.ViewModels;

public partial class InsightsViewModel : ObservableObject
{
    private readonly AgentService _agentService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial AgentInfo? SelectedAgent { get; set; }
    [ObservableProperty] public partial TranscriptDate? SelectedDate { get; set; }
    [ObservableProperty] public partial InsightResult? SelectedInsight { get; set; }

    public ObservableCollection<AgentInfo> Agents { get; } = [];
    public ObservableCollection<TranscriptDate> Dates { get; } = [];

    public InsightsViewModel(AgentService agentService, MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public void Load()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var agents = _agentService.ListAgents(CurrentSlug);
            Agents.Clear();
            foreach (var a in agents) Agents.Add(a);
            SelectedAgent = null;
            SelectedDate = null;
            SelectedInsight = null;
            Dates.Clear();
            if (agents.Count == 1) SelectAgent(agents[0]);
        }
        finally { IsLoading = false; }
    }

    public void SelectAgent(AgentInfo agent)
    {
        if (CurrentSlug is null) return;
        SelectedAgent = agent;
        SelectedDate = null;
        SelectedInsight = null;
        Dates.Clear();
        var dates = _agentService.ListTranscriptDates(CurrentSlug, agent.Slug);
        foreach (var d in dates) Dates.Add(d);
        if (dates.Length > 0) SelectDate(dates[0]);
    }

    public void SelectDate(TranscriptDate date)
    {
        if (CurrentSlug is null || SelectedAgent is null) return;
        SelectedDate = date;
        SelectedInsight = _agentService.ReadInsights(CurrentSlug, SelectedAgent.Slug, date);
    }
}
