using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class KnowledgeBasePage : Page
{
    public KnowledgeBaseViewModel ViewModel { get; }

    public KnowledgeBasePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<KnowledgeBaseViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void Article_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedArticle is { } article)
            await ViewModel.SelectArticleAsync(article);
    }

    private async void DeleteArticle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KbArticle article })
            await ViewModel.DeleteArticleCommand.ExecuteAsync(article);
    }
}
