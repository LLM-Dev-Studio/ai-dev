using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class ConsistencyPage : Page
{
    public ConsistencyViewModel ViewModel { get; }

    public ConsistencyPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ConsistencyViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RunScanAsync();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ConsistencyViewModel.WarningCount))
                WarningCountText.Text = ViewModel.WarningCount.ToString();
            if (e.PropertyName is nameof(ConsistencyViewModel.ErrorCount))
                ErrorCountText.Text = ViewModel.ErrorCount.ToString();
            if (e.PropertyName is nameof(ConsistencyViewModel.HasRun))
                NoIssuesText.Visibility = ViewModel.HasRun && ViewModel.Findings.Count == 0
                    ? Microsoft.UI.Xaml.Visibility.Visible
                    : Microsoft.UI.Xaml.Visibility.Collapsed;
        };
    }
}
