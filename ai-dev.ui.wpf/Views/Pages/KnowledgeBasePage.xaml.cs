using AiDev.Desktop.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiDev.Desktop.Views.Pages;

public partial class KnowledgeBasePage : Page
{
    private readonly KnowledgeBaseViewModel _viewModel;

    public KnowledgeBasePage(KnowledgeBaseViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void Article_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: KbArticle article })
            await _viewModel.SelectArticleAsync(article);
    }
}
