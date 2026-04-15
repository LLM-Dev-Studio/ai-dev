using AiDev.Features.Agent;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class TranscriptViewModel : ObservableObject
{
    private readonly AgentService _agentService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial AgentInfo? Agent { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial TranscriptDate? SelectedDate { get; set; }

    [ObservableProperty]
    public partial string TranscriptContent { get; set; } = "";
    public ObservableCollection<TranscriptDate> Dates { get; } = [];

    public TranscriptViewModel(AgentService agentService, MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _mainViewModel = mainViewModel;
    }

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
    }
}
