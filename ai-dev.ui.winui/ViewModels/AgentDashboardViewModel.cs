using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class AgentDashboardViewModel : ObservableObject, IDisposable
{
    private readonly AgentService _agentService;
    private readonly AgentRunnerService _agentRunnerService;
    private readonly AgentTemplatesService _templatesService;
    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue _dispatcher;
    private Timer? _pollTimer;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    public ObservableCollection<AgentCardViewModel> Agents { get; } = [];
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
        _dispatcher = DispatcherQueue.GetForCurrentThread();
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
            foreach (var a in agents)
                Agents.Add(new AgentCardViewModel(a, _agentRunnerService.IsRunning(CurrentSlug, a.Slug)));

            var templates = _templatesService.ListTemplates();
            Templates.Clear();
            foreach (var t in templates) Templates.Add(t);

            // Poll every 2 s to update live run state on cards
            _pollTimer ??= new Timer(_ =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (CurrentSlug is null) return;
                    foreach (var card in Agents)
                        card.IsRunning = _agentRunnerService.IsRunning(CurrentSlug, card.Agent.Slug);
                });
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RunAgentAsync(AgentCardViewModel card)
    {
        if (CurrentSlug is null) return;
        _agentRunnerService.LaunchAgent(CurrentSlug, card.Agent.Slug);
        card.IsRunning = true;
        await LoadAsync();
    }

    [RelayCommand]
    public async Task StopAgentAsync(AgentCardViewModel card)
    {
        if (CurrentSlug is null) return;
        _agentRunnerService.StopAgent(CurrentSlug, card.Agent.Slug);
        card.IsRunning = false;
        await LoadAsync();
    }

    [RelayCommand]
    public void SelectAgent(AgentCardViewModel card) => AgentSelected?.Invoke(card.Agent);

    /// <summary>Creates an agent from the provided template and display name. Returns an error message or null on success.</summary>
    public async Task<string?> CreateAgentAsync(string templateSlug, string name, string? slug = null)
    {
        if (CurrentSlug is null)
            return "No active project.";

        if (string.IsNullOrWhiteSpace(templateSlug))
            return "Template is required.";

        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";

        var resolvedSlug = string.IsNullOrWhiteSpace(slug)
            ? new string(name.Trim().ToLowerInvariant().Replace(" ", "-").Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray()).Trim('-')
            : slug.Trim();

        if (string.IsNullOrWhiteSpace(resolvedSlug))
            return "Agent slug is required.";

        var result = _agentService.CreateAgent(CurrentSlug, resolvedSlug, name.Trim(), templateSlug);
        if (result is Err<Unit> err)
            return err.Error.Message;

        await LoadAsync();
        return null;
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }
}
