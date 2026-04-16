using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class JournalsViewModel : ObservableObject
{
    private readonly JournalsService _journalsService;
    private readonly AgentService _agentService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private AgentInfo? _selectedAgent;
    [ObservableProperty] private JournalEntry? _selectedEntry;
    [ObservableProperty] private string _entryContent = "";

    public ObservableCollection<AgentInfo> Agents { get; } = [];
    public ObservableCollection<JournalEntry> Entries { get; } = [];

    public JournalsViewModel(
        JournalsService journalsService,
        AgentService agentService,
        MainViewModel mainViewModel)
    {
        _journalsService = journalsService;
        _agentService = agentService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var agents = _agentService.ListAgents(CurrentSlug);
            Agents.Clear();
            foreach (var a in agents) Agents.Add(a);

            if (SelectedAgent is not null)
                await LoadEntriesAsync(SelectedAgent);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectAgentAsync(AgentInfo agent)
    {
        SelectedAgent = agent;
        await LoadEntriesAsync(agent);
    }

    private async Task LoadEntriesAsync(AgentInfo agent)
    {
        if (CurrentSlug is null) return;
        var entries = _journalsService.ListDates(CurrentSlug, agent.Slug);
        Entries.Clear();
        foreach (var e in entries.OrderByDescending(e => e.Date))
            Entries.Add(e);
    }

    public async Task SelectEntryAsync(JournalEntry entry)
    {
        if (CurrentSlug is null || SelectedAgent is null) return;
        SelectedEntry = entry;
        EntryContent = _journalsService.GetEntry(CurrentSlug, SelectedAgent.Slug, entry.Date);
    }
}
