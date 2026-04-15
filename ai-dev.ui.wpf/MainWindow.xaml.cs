using AiDev.Desktop.ViewModels;
using AiDev.Desktop.Views.Pages;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AiDev.Desktop;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _services;
    private readonly MainViewModel _viewModel;
    private readonly ISnackbarService _snackbarService;

    public MainWindow(
        IServiceProvider services,
        MainViewModel viewModel,
        ISnackbarService snackbarService)
    {
        _services = services;
        _viewModel = viewModel;
        _snackbarService = snackbarService;

        DataContext = viewModel;

        InitializeComponent();
        ApplicationThemeManager.ApplySystemTheme();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        // Wire service provider so NavigationView can resolve pages from DI
        RootNavigation.SetServiceProvider(_services);

        RootNavigation.Navigate(typeof(ProjectsPage));
        _ = _viewModel.LoadExecutorStatusesAsync();
    }

    public void NavigateTo(Type pageType, AgentInfo? agentData = null)
    {
        // Store agent before navigating so the target page can read it in its Loaded handler
        _viewModel.PendingAgent = agentData;
        RootNavigation.Navigate(pageType);
    }

    public void SetStatus(string message)
        => Dispatcher.Invoke(() => StatusText.Text = message);

    public void ShowNotification(string title, string message, ControlAppearance appearance = ControlAppearance.Info)
        => _snackbarService.Show(title, message, appearance, null, TimeSpan.FromSeconds(3));

    public void NavigateToProject(ProjectDetail project)
    {
        _viewModel.SetActiveProject(project);
        RebuildProjectNav(project);
        RootNavigation.Navigate(typeof(AgentDashboardPage));
    }

    private void RebuildProjectNav(ProjectDetail project)
    {
        RootNavigation.MenuItems.Clear();

        RootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Content = "← Projects",
            Icon = new SymbolIcon(SymbolRegular.ArrowLeft24),
            Tag = "back"
        });

        var projectItems = new (string Label, SymbolRegular Icon, Type Page)[]
        {
            ("Agents",          SymbolRegular.PeopleTeam24,         typeof(AgentDashboardPage)),
            ("Board",           SymbolRegular.TaskListSquareLtr24,  typeof(BoardPage)),
            ("Messages",        SymbolRegular.Mail24,               typeof(MessagesPage)),
            ("Decisions",       SymbolRegular.Lightbulb24,          typeof(DecisionsPage)),
            ("Journals",        SymbolRegular.BookOpen24,           typeof(JournalsPage)),
            ("Knowledge Base",  SymbolRegular.Library24,            typeof(KnowledgeBasePage)),
            ("Secrets",         SymbolRegular.Key24,                typeof(SecretsPage)),
        };

        foreach (var (label, icon, pageType) in projectItems)
        {
            RootNavigation.MenuItems.Add(new NavigationViewItem
            {
                Content = label,
                Icon = new SymbolIcon(icon),
                TargetPageType = pageType
            });
        }

        RootNavigation.FooterMenuItems.Clear();
        RootNavigation.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = "Settings",
            Icon = new SymbolIcon(SymbolRegular.Settings24),
            TargetPageType = typeof(SettingsPage)
        });
    }
}
