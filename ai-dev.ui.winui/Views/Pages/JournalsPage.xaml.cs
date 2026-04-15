using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class JournalsPage : Page
{
    public JournalsViewModel ViewModel { get; }

    public JournalsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<JournalsViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void Agent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedAgent is { } agent)
            await ViewModel.SelectAgentAsync(agent);
    }

    private async void Entry_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedEntry is { } entry)
            await ViewModel.SelectEntryAsync(entry);
    }
}
