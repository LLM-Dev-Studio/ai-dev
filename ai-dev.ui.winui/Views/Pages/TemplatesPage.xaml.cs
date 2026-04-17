using AiDev.WinUI.ViewModels;

using Markdig;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using System.ComponentModel;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private WebView2? _previewWebView;

    public TemplatesViewModel ViewModel { get; }

    public TemplatesPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TemplatesViewModel>();
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
        RenderPreview(ViewModel.EditContent);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _previewWebView = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplatesViewModel.EditContent))
            RenderPreview(ViewModel.EditContent);
    }

    private void RenderPreview(string markdown)
    {
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
  pre, code {
    font-family: Consolas, "Cascadia Code", monospace;
    background: #1f1f1f;
    border-radius: 4px;
  }
  code { padding: 2px 4px; }
  pre { padding: 10px; overflow-x: auto; }
  a { color: #60a5fa; }
</style>
</head>
<body>
{{html}}
</body>
</html>
""";

        _previewWebView?.NavigateToString(document);
    }
}
