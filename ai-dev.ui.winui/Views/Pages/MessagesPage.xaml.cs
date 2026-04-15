using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class MessagesPage : Page
{
    public MessagesViewModel ViewModel { get; }

    public MessagesPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MessagesViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void ShowProcessed_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.LoadAsync();
    }

    private void Messages_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private async void MarkProcessed_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedMessage is { } msg)
            await ViewModel.MarkProcessedCommand.ExecuteAsync(msg);
    }
}
