using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Markdig;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class JournalsViewModel : ObservableObject
{
    private readonly JournalsService _journalsService;
    private readonly AgentService _agentService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial AgentInfo? SelectedAgent { get; set; }

    [ObservableProperty]
    public partial JournalEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string EntryContent { get; set; } = "";

    [ObservableProperty]
    public partial string EntryContentHtml { get; set; } = BuildHtmlDocument(string.Empty);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public ObservableCollection<AgentInfo> Agents { get; } = [];
    public ObservableCollection<JournalEntry> Entries { get; } = [];

    public JournalsViewModel(
        JournalsService journalsService,
        AgentService agentService,
        MainViewModel mainViewModel)
    {
        _journalsService = journalsService;
        _agentService = agentService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var agents = _agentService.ListAgents(CurrentSlug);
            Agents.Clear();
            foreach (var a in agents) Agents.Add(a);

            if (SelectedAgent is not null)
                await LoadEntriesAsync(SelectedAgent);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectAgentAsync(AgentInfo agent)
    {
        SelectedAgent = agent;
        await LoadEntriesAsync(agent);
    }

    private Task LoadEntriesAsync(AgentInfo agent)
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        var entries = _journalsService.ListDates(CurrentSlug, agent.Slug);
        Entries.Clear();
        foreach (var e in entries.OrderByDescending(e => e.Date))
            Entries.Add(e);
        return Task.CompletedTask;
    }

    public Task SelectEntryAsync(JournalEntry entry)
    {
        if (CurrentSlug is null || SelectedAgent is null) return Task.CompletedTask;
        SelectedEntry = entry;
        EntryContent = _journalsService.GetEntry(CurrentSlug, SelectedAgent.Slug, entry.Date);
        EntryContentHtml = BuildHtmlDocument(RenderMarkdown(EntryContent));
        return Task.CompletedTask;
    }

    private static string RenderMarkdown(string markdown)
        => Markdig.Markdown.ToHtml(markdown ?? string.Empty, Pipeline);

    private static string BuildHtmlDocument(string bodyHtml)
        => $$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8" />
<style>
  body {
    margin: 0;
    padding: 16px;
    font-family: "Segoe UI", sans-serif;
    font-size: 14px;
    line-height: 1.5;
    color: #e5e7eb;
    background: transparent;
  }
  h1, h2, h3, h4, h5, h6 { margin-top: 1.1em; margin-bottom: 0.4em; }
  p { margin: 0 0 0.8em 0; }
  pre {
    overflow: auto;
    padding: 10px;
    border-radius: 6px;
    background: #111827;
  }
  code { font-family: Consolas, "Courier New", monospace; }
  blockquote {
    margin: 0.8em 0;
    padding: 0.2em 0.8em;
    border-left: 3px solid #4b5563;
    color: #d1d5db;
  }
  a { color: #93c5fd; }
  table { border-collapse: collapse; width: 100%; }
  th, td { border: 1px solid #374151; padding: 6px; text-align: left; }
</style>
</head>
<body>
{{bodyHtml}}
</body>
</html>
""";
}

