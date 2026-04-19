using AiDev.WinUI.ViewModels;

using Markdig;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class MessagesPage : Page
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private WebView2? _messageBodyWebView;

    public MessagesViewModel ViewModel { get; }

    public MessagesPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MessagesViewModel>();
        DataContext = ViewModel;
        Loaded += OnLoadedAsync;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MessagesViewModel.SelectedMessage))
                RenderSelectedMessageBody();
        };
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        _messageBodyWebView = FindName("MessageBodyWebView") as WebView2;
        if (_messageBodyWebView is not null)
            await _messageBodyWebView.EnsureCoreWebView2Async();
        RenderSelectedMessageBody();
    }

    private async void ShowProcessed_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.LoadAsync();
    }

    private void Messages_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private async void FilterTaskId_TextChanged(object sender, TextChangedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void MarkProcessed_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedMessage is { } msg)
            await ViewModel.MarkProcessedCommand.ExecuteAsync(msg);
    }

    private void RenderSelectedMessageBody()
    {
        if (_messageBodyWebView?.CoreWebView2 is null)
            return;

        var markdown = ViewModel.SelectedMessage?.Body ?? string.Empty;
        var html = Markdown.ToHtml(markdown, MarkdownPipeline);
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
    background: transparent;
    overflow-wrap: anywhere;
  }
  h1, h2, h3, h4, h5, h6 { margin-top: 0.9em; }
  pre, code {
    font-family: Consolas, "Cascadia Code", monospace;
    background: #1f1f1f;
    border-radius: 4px;
  }
  code { padding: 2px 4px; }
  pre { padding: 10px; overflow-x: auto; }
  a { color: #60a5fa; }
  table { border-collapse: collapse; }
  th, td { border: 1px solid #3f3f46; padding: 4px 8px; }
</style>
</head>
<body>
{{html}}
</body>
</html>
""";

        _messageBodyWebView.NavigateToString(document);
    }
}
