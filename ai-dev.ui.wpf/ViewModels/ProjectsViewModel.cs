using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _newProjectName = "";
    [ObservableProperty] private string _newProjectSlug = "";
    [ObservableProperty] private string _newProjectDescription = "";
    [ObservableProperty] private string _newProjectCodebasePath = "";
    [ObservableProperty] private bool _isCreatingProject;

    public ObservableCollection<WorkspaceProject> Projects { get; } = [];

    public event Action<ProjectDetail>? ProjectSelected;

    public ProjectsViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var projects = _workspaceService.ListProjects();
            Projects.Clear();
            foreach (var p in projects)
                Projects.Add(p);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;

        var slug = string.IsNullOrWhiteSpace(NewProjectSlug)
            ? NewProjectName.Trim().ToLower().Replace(" ", "-")
            : NewProjectSlug.Trim();

        IsCreatingProject = true;
        try
        {
            var result = _workspaceService.CreateProject(
                slug,
                NewProjectName.Trim(),
                NewProjectDescription.Trim() is { Length: > 0 } d ? d : null,
                string.IsNullOrWhiteSpace(NewProjectCodebasePath) ? null : NewProjectCodebasePath.Trim());

            if (result is Ok<Unit>)
            {
                NewProjectName = "";
                NewProjectSlug = "";
                NewProjectDescription = "";
                NewProjectCodebasePath = "";
                await LoadAsync();
            }
        }
        finally
        {
            IsCreatingProject = false;
        }
    }

    [RelayCommand]
    public void OpenProject(WorkspaceProject project)
    {
        var detail = _workspaceService.GetProject(project.Slug);
        if (detail is not null)
            ProjectSelected?.Invoke(detail);
    }
}
