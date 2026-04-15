using AiDev.Features.Agent;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class AgentDashboardViewModel : ObservableObject
{
    private readonly AgentService _agentService;
    private readonly AgentRunnerService _agentRunnerService;
    private readonly AgentTemplatesService _templatesService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string NewAgentName { get; set; } = "";
    [ObservableProperty] public partial string NewAgentRole { get; set; } = "";
    [ObservableProperty] public partial bool IsAddingAgent { get; set; }

    public ObservableCollection<AgentInfo> Agents { get; } = [];
    public ObservableCollection<AgentTemplate> Templates { get; } = [];

    public event Action<AgentInfo>? AgentSelected;

    public AgentDashboardViewModel(
        AgentService agentService,
        AgentRunnerService agentRunnerService,
        AgentTemplatesService templatesService,
        MainViewModel mainViewModel)
    {
        _agentService = agentService;
        _agentRunnerService = agentRunnerService;
        _templatesService = templatesService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public Task LoadAsync()
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        IsLoading = true;
        try
        {
            var agents = _agentService.ListAgents(CurrentSlug);
            Agents.Clear();
            foreach (var a in agents) Agents.Add(a);

            var templates = _templatesService.ListTemplates();
            Templates.Clear();
            foreach (var t in templates) Templates.Add(t);
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RunAgentAsync(AgentInfo agent)
    {
        if (CurrentSlug is null) return;
        _agentRunnerService.LaunchAgent(CurrentSlug, agent.Slug);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task StopAgentAsync(AgentInfo agent)
    {
        if (CurrentSlug is null) return;
        _agentRunnerService.StopAgent(CurrentSlug, agent.Slug);
        await LoadAsync();
    }

    [RelayCommand]
    public void SelectAgent(AgentInfo agent) => AgentSelected?.Invoke(agent);

    [RelayCommand]
    public async Task CreateAgentAsync()
    {
        if (CurrentSlug is null || string.IsNullOrWhiteSpace(NewAgentName)) return;

        var agentSlug = NewAgentName.Trim().ToLower().Replace(" ", "-");
        IsAddingAgent = true;
        try
        {
            _agentService.CreateAgent(CurrentSlug, agentSlug, NewAgentName.Trim(), null);
            NewAgentName = "";
            NewAgentRole = "";
            IsAddingAgent = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            IsAddingAgent = false;
            System.Diagnostics.Debug.WriteLine($"Failed to create agent '{NewAgentName}': {ex.Message}");
        }
    }

    [RelayCommand]
    public void CancelAddAgent() => IsAddingAgent = false;
}
