using AiDev.Desktop.ViewModels;
using System.Windows.Controls;

namespace AiDev.Desktop.Views.Pages;

public partial class ProjectsPage : Page, IDisposable
{
    private readonly ProjectsViewModel _viewModel;

    public ProjectsPage(ProjectsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.ProjectSelected += OnProjectSelected;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void OnProjectSelected(ProjectDetail project)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateToProject(project);
    }

    public void Dispose()
    {
        _viewModel.ProjectSelected -= OnProjectSelected;
    }
}
