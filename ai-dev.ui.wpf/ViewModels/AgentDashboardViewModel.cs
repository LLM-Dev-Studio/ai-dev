using AiDev.Features.Agent;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class AgentDashboardViewModel : ObservableObject
{
    private readonly AgentService _agentService;
    private readonly AgentRunnerService _agentRunnerService;
    private readonly AgentTemplatesService _templatesService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _newAgentName = "";
    [ObservableProperty] private string _newAgentRole = "";
    [ObservableProperty] private bool _isAddingAgent;

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
    public async Task LoadAsync()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var agents = await Task.Run(() => _agentService.ListAgents(CurrentSlug));
            Agents.Clear();
            foreach (var a in agents)
                Agents.Add(a);

            var templates = await Task.Run(() => _templatesService.ListTemplates());
            Templates.Clear();
            foreach (var t in templates)
                Templates.Add(t);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void RunAgent(AgentInfo agent)
    {
        if (CurrentSlug is null) return;
        _agentRunnerService.LaunchAgent(CurrentSlug, agent.Slug);
        _ = LoadAsync();
    }

    [RelayCommand]
    public void StopAgent(AgentInfo agent)
    {
        if (CurrentSlug is null) return;
        _agentRunnerService.StopAgent(CurrentSlug, agent.Slug);
        _ = LoadAsync();
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
            await Task.Run(() => _agentService.CreateAgent(CurrentSlug, agentSlug, NewAgentName.Trim(), null));
            NewAgentName = "";
            NewAgentRole = "";
            IsAddingAgent = false;
            await LoadAsync();
        }
        catch
        {
            IsAddingAgent = false;
        }
    }

    [RelayCommand]
    public void CancelAddAgent() => IsAddingAgent = false;
}
