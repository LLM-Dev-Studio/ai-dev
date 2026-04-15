using AiDev.Features.Agent;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.Desktop.ViewModels;

public partial class AgentDetailViewModel : ObservableObject
{
    private readonly AgentService _agentService;
    private readonly AgentRunnerService _agentRunnerService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private AgentInfo? _agent;
    [ObservableProperty] private bool _isLoading;

    public event Action? NavigateToTranscript;

    public AgentDetailViewModel(
        AgentService agentService,
        AgentRunnerService agentRunnerService,
        MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _agentRunnerService = agentRunnerService;
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
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RunAsync()
    {
        if (CurrentSlug is null || Agent is null) return;
        _agentRunnerService.LaunchAgent(CurrentSlug, Agent.Slug);
        await LoadAsync(Agent.Slug);
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        if (CurrentSlug is null || Agent is null) return;
        _agentRunnerService.StopAgent(CurrentSlug, Agent.Slug);
        await LoadAsync(Agent.Slug);
    }

    [RelayCommand]
    public void ViewTranscript() => NavigateToTranscript?.Invoke();
}
