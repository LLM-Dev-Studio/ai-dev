using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class TranscriptPage : Page
{
    public TranscriptViewModel ViewModel { get; }

    public TranscriptPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TranscriptViewModel>();
        DataContext = ViewModel;

        ViewModel.NavigateBack += () =>
        {
            var window = App.Services.GetRequiredService<MainWindow>();
            window.NavigateTo("agent-detail");
        };

        Loaded += async (_, _) =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            if (mainVm.PendingAgent is AgentInfo agent)
                await ViewModel.LoadAsync(agent.Slug);
        };
    }

    private async void Date_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedDate is { } date)
            await ViewModel.SelectDateCommand.ExecuteAsync(date);
    }
}
