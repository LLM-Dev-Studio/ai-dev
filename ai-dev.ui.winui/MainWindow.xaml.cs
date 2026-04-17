using AiDev.WinUI.ViewModels;
using AiDev.WinUI.Views.Pages;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private readonly Dictionary<string, Type> _pageMap = new()
    {
        ["projects"] = typeof(ProjectsPage),
        ["agents"] = typeof(AgentDashboardPage),
        ["board"] = typeof(BoardPage),
        ["messages"] = typeof(MessagesPage),
        ["decisions"] = typeof(DecisionsPage),
        ["decision-detail"] = typeof(DecisionDetailPage),
        ["journals"] = typeof(JournalsPage),
        ["kb"] = typeof(KnowledgeBasePage),
        ["secrets"] = typeof(SecretsPage),
        ["settings"] = typeof(SettingsPage),
        ["templates"] = typeof(TemplatesPage),
        ["transcript"] = typeof(TranscriptPage),
        ["detail"] = typeof(AgentDetailPage),
        ["digest"] = typeof(DigestPage),
        ["insights"] = typeof(InsightsPage),
        ["consistency"] = typeof(ConsistencyPage),
        ["codebase"] = typeof(CodebasePage),
        ["project-settings"] = typeof(ProjectSettingsPage),
        ["process"] = typeof(ProcessPage),
    };

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        ExecutorStatusList.ItemsSource = _viewModel.ExecutorStatuses;

        // Navigate to projects on launch
        RootNavigation.SelectedItem = ProjectsNavItem;
        ContentFrame.Navigate(typeof(ProjectsPage));

        try
        {
            await _viewModel.LoadExecutorStatusesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load executor statuses: {ex.Message}");
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer?.Tag is string tag && _pageMap.TryGetValue(tag, out var pageType))
        {
            ContentFrame.Navigate(pageType);
        }
    }

    public void NavigateToProject(ProjectDetail project)
    {
        _viewModel.SetActiveProject(project);
        Title = $"AI Dev Net — {project.Name}";
        RebuildProjectNav(project);
        ContentFrame.Navigate(typeof(AgentDashboardPage));
    }

    public void NavigateTo(string pageKey, object? parameter = null)
    {
        if (_pageMap.TryGetValue(pageKey, out var pageType))
            ContentFrame.Navigate(pageType, parameter);
    }

    public void SetStatus(string message) => StatusText.Text = message;

    private void RebuildProjectNav(ProjectDetail project)
    {
        RootNavigation.MenuItems.Clear();

        RootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Content = "← Projects",
            Tag = "projects",
            Icon = new SymbolIcon(Symbol.Back)
        });

        var items = new (string Label, Symbol Icon, string Key)[]
        {
            ("Agents",         Symbol.People,           "agents"),
            ("Board",          Symbol.ViewAll,          "board"),
            ("Messages",       Symbol.Mail,             "messages"),
            ("Decisions",      Symbol.Important,        "decisions"),
            ("Journals",       Symbol.Document,         "journals"),
            ("Knowledge Base", Symbol.Library,          "kb"),
            ("Secrets",        Symbol.ProtectedDocument,"secrets"),
            ("Digest",         Symbol.List,             "digest"),
            ("Insights",       Symbol.Bullets,          "insights"),
            ("Codebase",       Symbol.Folder,           "codebase"),
            ("Consistency",    Symbol.Accept,           "consistency"),
            ("Process",        Symbol.List,             "process"),
            ("Settings",       Symbol.Setting,          "project-settings"),
        };

        foreach (var (label, icon, key) in items)
        {
            RootNavigation.MenuItems.Add(new NavigationViewItem
            {
                Content = label,
                Tag = key,
                Icon = new SymbolIcon(icon)
            });
        }

        RootNavigation.FooterMenuItems.Clear();
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Templates",
            Tag = "templates",
            Icon = new SymbolIcon(Symbol.Library)
        });
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Settings",
            Tag = "settings",
            Icon = new SymbolIcon(Symbol.Setting)
        });
    }
}
