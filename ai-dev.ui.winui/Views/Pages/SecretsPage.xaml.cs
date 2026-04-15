using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class SecretsPage : Page
{
    public SecretsViewModel ViewModel { get; }

    public SecretsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SecretsViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private void DeleteSecret_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name })
            ViewModel.RequestDeleteCommand.Execute(name);
    }
}
