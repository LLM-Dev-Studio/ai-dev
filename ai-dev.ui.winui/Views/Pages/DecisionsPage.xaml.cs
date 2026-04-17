using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class DecisionsPage : Page
{
    public DecisionsViewModel ViewModel { get; }

    public DecisionsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DecisionsViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void ShowResolved_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.LoadAsync();
    }

    private async void Decisions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedDecision is { } decision)
            await ViewModel.SelectDecisionAsync(decision);
    }

    private void OpenDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            mainVm.PendingDecisionId = id;
            var window = App.Services.GetRequiredService<MainWindow>();
            window.NavigateTo("decision-detail");
        }
    }
}
