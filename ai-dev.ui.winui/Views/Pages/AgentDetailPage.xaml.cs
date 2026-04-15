using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class AgentDetailPage : Page
{
    public AgentDetailViewModel ViewModel { get; }

    public AgentDetailPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AgentDetailViewModel>();
        DataContext = ViewModel;

        ViewModel.NavigateToTranscript += OnNavigateToTranscript;
        ViewModel.Inbox.CollectionChanged += (_, _) =>
            InboxEmptyText.Visibility = ViewModel.Inbox.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

        Loaded += async (_, _) =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            if (mainVm.PendingAgent is { } agent)
                await ViewModel.LoadAsync(agent.Slug);
        };

        Unloaded += (_, _) =>
        {
            ViewModel.NavigateToTranscript -= OnNavigateToTranscript;
            ViewModel.Dispose();
        };
    }

    private void OnNavigateToTranscript()
    {
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        mainVm.PendingAgent = ViewModel.Agent;
        var window = App.Services.GetRequiredService<MainWindow>();
        window.NavigateTo("transcript");
    }

    private void ToggleClaude_Click(object sender, RoutedEventArgs e)
        => ViewModel.ClaudeExpanded = !ViewModel.ClaudeExpanded;
}
