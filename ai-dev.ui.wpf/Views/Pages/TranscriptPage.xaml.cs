using AiDev.Desktop.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiDev.Desktop.Views.Pages;

public partial class TranscriptPage : Page, IDisposable
{
    private readonly TranscriptViewModel _viewModel;
    private readonly MainViewModel _mainViewModel;

    public TranscriptPage(TranscriptViewModel viewModel, MainViewModel mainViewModel)
    {
        _viewModel = viewModel;
        _mainViewModel = mainViewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_mainViewModel.PendingAgent is { } agent)
        {
            await _viewModel.LoadAsync(agent.Slug);
            _mainViewModel.PendingAgent = null;
        }
    }

    private async void Date_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: TranscriptDate date })
            await _viewModel.SelectDateAsync(date);
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;
    }
}
