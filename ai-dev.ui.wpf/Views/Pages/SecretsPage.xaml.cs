using AiDev.Desktop.ViewModels;
using System.Windows.Controls;

namespace AiDev.Desktop.Views.Pages;

public partial class SecretsPage : Page
{
    private readonly SecretsViewModel _viewModel;

    public SecretsPage(SecretsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }
}
