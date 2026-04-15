using AiDev.Desktop.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiDev.Desktop.Views.Pages;

public partial class DecisionsPage : Page
{
    private readonly DecisionsViewModel _viewModel;

    public DecisionsPage(DecisionsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void DecisionItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: DecisionItem decision })
            await _viewModel.SelectDecisionAsync(decision);
    }
}
