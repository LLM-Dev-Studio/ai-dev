using AiDev.Desktop.ViewModels;
using System.Windows.Controls;

namespace AiDev.Desktop.Views.Pages;

public partial class BoardPage : Page
{
    private readonly BoardViewModel _viewModel;

    public BoardPage(BoardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }
}
