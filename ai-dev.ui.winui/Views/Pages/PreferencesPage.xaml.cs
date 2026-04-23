using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class PreferencesPage : Page
{
    public PreferencesViewModel ViewModel { get; }

    public PreferencesPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<PreferencesViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }
}
