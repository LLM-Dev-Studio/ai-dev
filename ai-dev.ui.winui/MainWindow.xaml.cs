using AiDev.WinUI.ViewModels;
using AiDev.WinUI.Views.Pages;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WinRT.Interop;

namespace AiDev.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Kept so we can update InfoBadge values reactively
    private NavigationViewItem? _messagesNavItem;
    private NavigationViewItem? _decisionsNavItem;

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
        ["task-transcript"] = typeof(TaskTranscriptPage),
        ["detail"] = typeof(AgentDetailPage),
        ["digest"] = typeof(DigestPage),
        ["insights"] = typeof(InsightsPage),
        ["consistency"] = typeof(ConsistencyPage),
        ["codebase"] = typeof(CodebasePage),
        ["project-settings"] = typeof(ProjectSettingsPage),
        ["process"] = typeof(ProcessPage),
        ["preferences"] = typeof(PreferencesPage),
    };

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetWindowIcon();
        Activated += OnActivated;
        Closed += (_, _) => _viewModel.StopLiveHealthUpdates();

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.UnreadMessageCount) or nameof(MainViewModel.PendingDecisionCount))
                UpdateNavBadges();
        };
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        ExecutorStatusList.ItemsSource = _viewModel.ExecutorStatuses;
        UpdatePaneStateVisuals();

        NavigateHome();

        try
        {
            await _viewModel.LoadExecutorStatusesAsync();
            _viewModel.StartLiveHealthUpdates(DispatcherQueue);
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
        _viewModel.RefreshNavBadges();
        ContentFrame.Navigate(typeof(AgentDashboardPage));
    }

    private void UpdateNavBadges()
    {
        if (_messagesNavItem != null)
        {
            _messagesNavItem.InfoBadge = _viewModel.UnreadMessageCount > 0
                ? new InfoBadge { Value = _viewModel.UnreadMessageCount }
                : null;
        }
        if (_decisionsNavItem != null)
        {
            _decisionsNavItem.InfoBadge = _viewModel.PendingDecisionCount > 0
                ? new InfoBadge { Value = _viewModel.PendingDecisionCount }
                : null;
        }
    }

    private void HomeHeader_Click(object sender, RoutedEventArgs e)
        => NavigateHome();

    private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
    {
        RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;
        if (RootNavigation.IsPaneOpen)
        {
            RootNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        }

        UpdatePaneStateVisuals();
    }

    private void RootNavigation_PaneOpened(NavigationView sender, object args)
        => UpdatePaneStateVisuals();

    private void RootNavigation_PaneClosed(NavigationView sender, object args)
        => UpdatePaneStateVisuals();

    private void UpdatePaneStateVisuals()
    {
        if (RootNavigation.PaneFooter is FrameworkElement footer)
        {
            footer.Visibility = RootNavigation.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void NavigateHome()
    {
        _viewModel.SetActiveProject(null);
        _viewModel.PendingAgent = null;
        _viewModel.PendingTaskId = null;
        _viewModel.PendingDecisionId = null;

        Title = "AI Dev Net";

        RootNavigation.MenuItems.Clear();
        RootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Content = "Projects",
            Tag = "projects",
            Icon = new SymbolIcon(Symbol.Home)
        });

        RootNavigation.FooterMenuItems.Clear();
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Templates",
            Tag = "templates",
            Icon = new SymbolIcon(Symbol.Library)
        });
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Preferences",
            Tag = "preferences",
            Icon = new SymbolIcon(Symbol.Manage)
        });
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Settings",
            Tag = "settings",
            Icon = new SymbolIcon(Symbol.Setting)
        });

        ContentFrame.Navigate(typeof(ProjectsPage));
    }

    public void NavigateTo(string pageKey, object? parameter = null)
    {
        if (_pageMap.TryGetValue(pageKey, out var pageType))
            ContentFrame.Navigate(pageType, parameter);
    }

    public void SetStatus(string message) => StatusText.Text = message;

    private void SetWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (!File.Exists(iconPath))
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(iconPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set window icon: {ex.Message}");
        }
    }

    private void RebuildProjectNav(ProjectDetail project)
    {
        _messagesNavItem = null;
        _decisionsNavItem = null;

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
            ("Digest",         Symbol.List,             "digest"),
            ("Insights",       Symbol.Bullets,          "insights"),
            ("Journals",       Symbol.Document,         "journals"),
            ("Knowledge Base", Symbol.Library,          "kb"),
            ("Codebase",       Symbol.Folder,           "codebase"),
            ("Consistency",    Symbol.Accept,           "consistency"),
            ("Process",        Symbol.List,             "process"),
            ("Secrets",        Symbol.ProtectedDocument,"secrets"),
            ("Settings",       Symbol.Setting,          "project-settings"),
        };

        foreach (var (label, icon, key) in items)
        {
            var item = new NavigationViewItem
            {
                Content = label,
                Tag = key,
                Icon = new SymbolIcon(icon)
            };
            RootNavigation.MenuItems.Add(item);

            if (key == "messages") _messagesNavItem = item;
            if (key == "decisions") _decisionsNavItem = item;
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
            Content = "Preferences",
            Tag = "preferences",
            Icon = new SymbolIcon(Symbol.Manage)
        });
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Settings",
            Tag = "settings",
            Icon = new SymbolIcon(Symbol.Setting)
        });
    }
}
