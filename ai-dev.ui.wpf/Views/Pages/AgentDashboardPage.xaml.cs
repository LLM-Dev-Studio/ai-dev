using AiDev.Desktop.ViewModels;
using System.Windows.Controls;

namespace AiDev.Desktop.Views.Pages;

public partial class AgentDashboardPage : Page
{
    private readonly AgentDashboardViewModel _viewModel;

    public AgentDashboardPage(AgentDashboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.AgentSelected += OnAgentSelected;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void OnAgentSelected(AgentInfo agent)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(typeof(AgentDetailPage), agent);
    }
}
