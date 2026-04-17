using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    public TemplatesViewModel ViewModel { get; }

    public TemplatesPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TemplatesViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }
}
