using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.WinUI.ViewModels;

public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
    private readonly AgentService _agentService;
    private readonly AgentTemplatesService _templatesService;
    private readonly MainViewModel _mainViewModel;

    // Project detail form
    [ObservableProperty] public partial string EditName { get; set; } = "";
    [ObservableProperty] public partial string EditDescription { get; set; } = "";
    [ObservableProperty] public partial string EditCodebasePath { get; set; } = "";
    [ObservableProperty] public partial string DetailsError { get; set; } = "";
    [ObservableProperty] public partial bool DetailsSaved { get; set; }
    [ObservableProperty] public partial bool SavingDetails { get; set; }

    // Add-agent form
    [ObservableProperty] public partial bool ShowAddAgent { get; set; }
    [ObservableProperty] public partial string NewAgentSlug { get; set; } = "";
    [ObservableProperty] public partial string NewAgentName { get; set; } = "";
    [ObservableProperty] public partial string NewAgentTemplate { get; set; } = "";
    [ObservableProperty] public partial string AgentError { get; set; } = "";

    public ObservableCollection<AgentInfo> Agents { get; } = [];
    public ObservableCollection<AgentTemplate> Templates { get; } = [];

    public ProjectSettingsViewModel(
        WorkspaceService workspaceService,
        AgentService agentService,
        AgentTemplatesService templatesService,
        MainViewModel mainViewModel)
    {
        _workspaceService = workspaceService;
        _agentService = agentService;
        _templatesService = templatesService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public void Load()
    {
        if (CurrentSlug is null) return;
        var project = _workspaceService.GetProject(CurrentSlug);
        if (project is not null)
        {
            EditName = project.Name;
            EditDescription = project.Description;
            EditCodebasePath = project.CodebasePath ?? "";
        }
        RefreshAgents();
        Templates.Clear();
        foreach (var t in _templatesService.ListTemplates()) Templates.Add(t);
    }

    [RelayCommand]
    public void SaveDetails()
    {
        if (CurrentSlug is null) return;
        DetailsError = "";
        DetailsSaved = false;
        if (string.IsNullOrWhiteSpace(EditName)) { DetailsError = "Name is required."; return; }
        SavingDetails = true;
        var result = _workspaceService.UpdateProject(CurrentSlug, EditName, EditDescription,
            string.IsNullOrWhiteSpace(EditCodebasePath) ? null : EditCodebasePath);
        SavingDetails = false;
        if (result is Err<Unit> err) { DetailsError = err.Error.Message; return; }
        // Sync the shell title
        var updated = _workspaceService.GetProject(CurrentSlug);
        if (updated is not null) _mainViewModel.SetActiveProject(updated);
        DetailsSaved = true;
    }

    [RelayCommand]
    public void OpenAddAgent() => ShowAddAgent = true;

    [RelayCommand]
    public void AddAgent()
    {
        if (CurrentSlug is null) return;
        AgentError = "";
        if (string.IsNullOrWhiteSpace(NewAgentSlug)) { AgentError = "Slug is required."; return; }
        if (string.IsNullOrWhiteSpace(NewAgentName)) { AgentError = "Name is required."; return; }
        var templateSlug = string.IsNullOrWhiteSpace(NewAgentTemplate) ? null : NewAgentTemplate;
        var result = _agentService.CreateAgent(CurrentSlug, NewAgentSlug, NewAgentName, templateSlug);
        if (result is Err<Unit> err) { AgentError = err.Error.Message; return; }
        RefreshAgents();
        ShowAddAgent = false;
        NewAgentSlug = "";
        NewAgentName = "";
        NewAgentTemplate = "";
    }

    [RelayCommand]
    public void CancelAddAgent()
    {
        ShowAddAgent = false;
        AgentError = "";
    }

    private void RefreshAgents()
    {
        if (CurrentSlug is null) return;
        Agents.Clear();
        foreach (var a in _agentService.ListAgents(CurrentSlug)) Agents.Add(a);
    }
}
