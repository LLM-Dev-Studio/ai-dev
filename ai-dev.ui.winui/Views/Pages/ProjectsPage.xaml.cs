using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class ProjectsPage : Page
{
    public ProjectsViewModel ViewModel { get; }

    public ProjectsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ProjectsViewModel>();
        DataContext = ViewModel;

        ViewModel.ProjectSelected += OnProjectSelected;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private void OnProjectSelected(ProjectDetail project)
    {
        if (App.Services.GetRequiredService<MainWindow>() is MainWindow w)
            w.NavigateToProject(project);
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorkspaceProject project })
            ViewModel.OpenProject(project);
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        // Delete not yet supported by WorkspaceService
    }

    private async void CreateProject_Click(object sender, RoutedEventArgs e)
        => await ViewModel.CreateProjectCommand.ExecuteAsync(null);
}
