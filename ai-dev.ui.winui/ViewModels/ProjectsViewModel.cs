using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string NewProjectName { get; set; } = "";
    [ObservableProperty] public partial string NewProjectSlug { get; set; } = "";
    [ObservableProperty] public partial string NewProjectDescription { get; set; } = "";
    [ObservableProperty] public partial string NewProjectCodebasePath { get; set; } = "";
    [ObservableProperty] public partial bool IsCreatingProject { get; set; }

    public ObservableCollection<WorkspaceProject> Projects { get; } = [];

    public event Action<ProjectDetail>? ProjectSelected;

    public ProjectsViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

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
            else if (result is Err<Unit> err)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create project '{slug}': [{err.Error.Code}] {err.Error.Message}");
            }
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
