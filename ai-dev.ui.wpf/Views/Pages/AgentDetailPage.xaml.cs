using AiDev.Desktop.ViewModels;
using System.Windows.Controls;

namespace AiDev.Desktop.Views.Pages;

public partial class AgentDetailPage : Page
{
    private readonly AgentDetailViewModel _viewModel;
    private readonly MainViewModel _mainViewModel;

    public AgentDetailPage(AgentDetailViewModel viewModel, MainViewModel mainViewModel)
    {
        _viewModel = viewModel;
        _mainViewModel = mainViewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.NavigateToTranscript += OnNavigateToTranscript;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_mainViewModel.PendingAgent is { } agent)
        {
            _ = _viewModel.LoadAsync(agent.Slug);
            _mainViewModel.PendingAgent = null;
        }
    }

    private void OnNavigateToTranscript()
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.NavigateTo(typeof(TranscriptPage), _viewModel.Agent);
    }
}
