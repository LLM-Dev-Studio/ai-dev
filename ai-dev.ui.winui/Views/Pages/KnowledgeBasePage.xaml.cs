using AiDev.WinUI.ViewModels;

using Markdig;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using System.ComponentModel;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class KnowledgeBasePage : Page
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private WebView2? _previewWebView;

    public KnowledgeBaseViewModel ViewModel { get; }

    public KnowledgeBasePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<KnowledgeBaseViewModel>();
        DataContext = ViewModel;

        Loaded += OnLoadedAsync;
        Unloaded += OnUnloaded;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();

        _previewWebView = FindName("PreviewWebView") as WebView2;
        if (_previewWebView is not null)
            await _previewWebView.EnsureCoreWebView2Async();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RenderPreview(ViewModel.ArticleContent);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _previewWebView = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KnowledgeBaseViewModel.ArticleContent))
            RenderPreview(ViewModel.ArticleContent);
    }

    private void RenderPreview(string markdown)
    {
        if (_previewWebView?.CoreWebView2 is null)
            return;

        var html = Markdown.ToHtml(markdown ?? string.Empty, MarkdownPipeline);
        var document = $$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8" />
<style>
  :root { color-scheme: dark; }
  body {
    margin: 0;
    padding: 12px;
    font-family: "Segoe UI", sans-serif;
    font-size: 14px;
    line-height: 1.5;
    color: #e5e7eb;
    background: #111111;
  }
  h1, h2, h3, h4, h5, h6 { margin-top: 1.1em; margin-bottom: 0.4em; }
  p { margin: 0 0 0.8em 0; }
  pre, code {
    font-family: Consolas, "Cascadia Code", monospace;
    background: #1f1f1f;
    border-radius: 4px;
  }
  code { padding: 2px 4px; }
  pre { padding: 10px; overflow-x: auto; }
  blockquote {
    margin: 0.8em 0;
    padding: 0.2em 0.8em;
    border-left: 3px solid #4b5563;
    color: #d1d5db;
  }
  a { color: #60a5fa; }
  table { border-collapse: collapse; width: 100%; }
  th, td { border: 1px solid #374151; padding: 6px; text-align: left; }
</style>
</head>
<body>
{{html}}
</body>
</html>
""";

        _previewWebView.NavigateToString(document);
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
