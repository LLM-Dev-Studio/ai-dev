using AiDev.Features.Agent;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
    private readonly AgentTemplatesService _templatesService;
    private readonly AgentService _agentService;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsCreatingProject { get; set; }

    public ObservableCollection<WorkspaceProject> Projects { get; } = [];

    public event Action<ProjectDetail>? ProjectSelected;

    public ProjectsViewModel(WorkspaceService workspaceService, AgentTemplatesService templatesService, AgentService agentService)
    {
        _workspaceService = workspaceService;
        _templatesService = templatesService;
        _agentService = agentService;
    }

    public List<AgentTemplate> GetTemplates() => _templatesService.ListTemplates();

    [RelayCommand]
    public Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var projects = _workspaceService.ListProjects();
            Projects.Clear();
            foreach (var p in projects)
                Projects.Add(p);
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Creates a project and its selected agents. Returns an error message or null on success.</summary>
    public async Task<string?> CreateProjectAsync(
        string slug, string name, string description,
        string? codebasePath, Dictionary<string, string> selectedTemplates)
    {
        IsCreatingProject = true;
        try
        {
            var result = _workspaceService.CreateProject(
                slug, name,
                description.Trim() is { Length: > 0 } d ? d : null,
                string.IsNullOrWhiteSpace(codebasePath) ? null : codebasePath.Trim());

            if (result is Err<Unit> err)
                return $"[{err.Error.Code}] {err.Error.Message}";

            foreach (var (templateSlug, agentName) in selectedTemplates)
            {
                var agentResult = _agentService.CreateAgent(slug, templateSlug, agentName, templateSlug);
                if (agentResult is Err<Unit> agentErr)
                    return $"[{agentErr.Error.Code}] {agentErr.Error.Message}";
            }

            await LoadAsync();
            return null;
        }
        finally
        {
            IsCreatingProject = false;
        }
    }

    public void OpenProject(WorkspaceProject project)
    {
        var detail = _workspaceService.GetProject(project.Slug);
        if (detail is not null)
            ProjectSelected?.Invoke(detail);
    }
}
