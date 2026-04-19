using AiDev.WinUI.ViewModels;

using Markdig;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class DecisionsPage : Page
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private WebView2? _decisionContentWebView;

    public DecisionsViewModel ViewModel { get; }

    public DecisionsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DecisionsViewModel>();
        DataContext = ViewModel;
        Loaded += OnLoadedAsync;
        Unloaded += (_, _) => ViewModel.StopPollingDecision();

        ViewModel.ChatMessages.CollectionChanged += (_, _) => RenderDecisionConversation();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DecisionsViewModel.SelectedDecision))
                RenderDecisionConversation();
        };
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        _decisionContentWebView = FindName("DecisionContentWebView") as WebView2;
        if (_decisionContentWebView is not null)
            await _decisionContentWebView.EnsureCoreWebView2Async();
        RenderDecisionConversation();
    }

    private async void ShowResolved_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.LoadAsync();
        RenderDecisionConversation();
    }

    private async void Decisions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedDecision is { } decision)
        {
            await ViewModel.SelectDecisionAsync(decision);
            RenderDecisionConversation();
        }
        else
        {
            RenderDecisionConversation();
        }
    }

    private void OpenDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            mainVm.PendingDecisionId = id;
            var window = App.Services.GetRequiredService<MainWindow>();
            window.NavigateTo("decision-detail");
        }
    }

    private void RenderDecisionConversation()
    {
        if (_decisionContentWebView?.CoreWebView2 is null)
            return;

        var subject = ViewModel.SelectedDecision?.Subject ?? "";
        var bodyMarkdown = ViewModel.SelectedDecision?.Body ?? "";
        var bodyHtml = Markdown.ToHtml(bodyMarkdown, MarkdownPipeline);

        var chatHtml = string.Join("", ViewModel.ChatMessages.Select(m =>
        {
            var from = System.Net.WebUtility.HtmlEncode(m.From);
            var messageHtml = Markdown.ToHtml(m.Content ?? string.Empty, MarkdownPipeline);
            return $$"""
<div class="msg">
  <div class="from">{{from}}</div>
  <div class="content">{{messageHtml}}</div>
</div>
""";
        }));

        var emptyState = ViewModel.SelectedDecision is null
            ? "<div class='empty'>Select a decision to view details and chat.</div>"
            : string.Empty;

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
    overflow-wrap: anywhere;
  }
  .subject {
    font-size: 16px;
    font-weight: 600;
    margin: 0 0 8px 0;
  }
  .decision {
    margin: 0 0 12px 0;
    padding-bottom: 12px;
    border-bottom: 1px solid #2b2b2b;
  }
  .msg {
    margin: 0 0 8px 0;
    padding: 10px;
    border: 1px solid #2b2b2b;
    border-radius: 6px;
    background: #161616;
  }
  .from {
    color: #a1a1aa;
    font-size: 12px;
    margin-bottom: 4px;
  }
  .empty {
    color: #a1a1aa;
  }
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
{{emptyState}}
<div class="decision">
  <div class="subject">{{System.Net.WebUtility.HtmlEncode(subject)}}</div>
  <div>{{bodyHtml}}</div>
</div>
{{chatHtml}}
</body>
</html>
""";

        _decisionContentWebView.NavigateToString(document);
    }
}
