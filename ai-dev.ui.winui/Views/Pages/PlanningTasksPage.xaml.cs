using AiDev.Features.Planning;
using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Windows.Storage.Pickers;
using Windows.Storage;

using WinRT.Interop;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class PlanningTasksPage : Page
{
    public PlanningTasksViewModel ViewModel { get; }

    public PlanningTasksPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<PlanningTasksViewModel>();
        DataContext = ViewModel;
        Loaded   += OnLoaded;
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Load();
        UpdatePhaseVisuals();
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PlanningTasksViewModel.CurrentPhase)
                                  or nameof(PlanningTasksViewModel.Phase1Locked)
                                  or nameof(PlanningTasksViewModel.Phase2Locked)
                                  or nameof(PlanningTasksViewModel.Phase1TurnCount)
                                  or nameof(PlanningTasksViewModel.Phase2TurnCount)
                                  or nameof(PlanningTasksViewModel.Phase3TurnCount))
            {
                UpdatePhaseVisuals();
            }
        };
    }

    // ── Phase visual updates ──────────────────────────────────────────────────

    private void UpdatePhaseVisuals()
    {
        var phase = ViewModel.CurrentPhase;

        // Phase accent bar colour
        var accentColor = phase switch
        {
            SessionPhase.Phase1BusinessDiscovery     => Windows.UI.Color.FromArgb(255, 0,   120, 212), // #0078D4 blue
            SessionPhase.Phase2SolutionShaping       => Windows.UI.Color.FromArgb(255, 16,  124, 16),  // #107C10 green
            SessionPhase.Phase3PlanningDecomposition => Windows.UI.Color.FromArgb(255, 134, 0,   77),  // #86004D purple
            _ => Windows.UI.Color.FromArgb(255, 100, 100, 100),
        };
        PhaseAccentBar.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(accentColor);

        // Phase title + role badge
        PhaseTitleText.Text = phase switch
        {
            SessionPhase.Phase1BusinessDiscovery     => "Phase 1 — Business Discovery",
            SessionPhase.Phase2SolutionShaping       => "Phase 2 — Solution Shaping",
            SessionPhase.Phase3PlanningDecomposition => "Phase 3 — Planning & Decomposition",
            _ => "",
        };
        PhaseRoleBadge.Text = phase switch
        {
            SessionPhase.Phase1BusinessDiscovery     => "Role: Business Analyst",
            SessionPhase.Phase2SolutionShaping       => "Role: Solution Architect",
            SessionPhase.Phase3PlanningDecomposition => "Role: Planning Assistant",
            _ => "",
        };

        // Phase 2 placeholder when locked
        if (phase == SessionPhase.Phase2SolutionShaping && !ViewModel.Phase1Locked)
        {
            PhaseTitleText.Text  = "Phase 2 — Solution Shaping";
            PhaseRoleBadge.Text  = "Phase 1 must be locked before proceeding.";
        }

        // Sidebar phase dots (green = current, grey = future, tick = locked)
        Phase1Dot.Fill  = GetPhaseDotBrush(SessionPhase.Phase1BusinessDiscovery, phase, ViewModel.Phase1Locked);
        Phase2Dot.Fill  = GetPhaseDotBrush(SessionPhase.Phase2SolutionShaping, phase, ViewModel.Phase2Locked);
        Phase3Dot.Fill  = GetPhaseDotBrush(SessionPhase.Phase3PlanningDecomposition, phase, false);

        Phase1Label.FontWeight = phase == SessionPhase.Phase1BusinessDiscovery
            ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        Phase2Label.FontWeight = phase == SessionPhase.Phase2SolutionShaping
            ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        Phase3Label.FontWeight = phase == SessionPhase.Phase3PlanningDecomposition
            ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

        // Turn count labels
        Phase1TurnText.Text = $"{ViewModel.Phase1TurnCount} turn{(ViewModel.Phase1TurnCount == 1 ? "" : "s")}";
        Phase2TurnText.Text = $"{ViewModel.Phase2TurnCount} turn{(ViewModel.Phase2TurnCount == 1 ? "" : "s")}";
        Phase3TurnText.Text = $"{ViewModel.Phase3TurnCount} turn{(ViewModel.Phase3TurnCount == 1 ? "" : "s")}";
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush GetPhaseDotBrush(
        SessionPhase thisPhase, SessionPhase currentPhase, bool isLocked)
    {
        if (isLocked)
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 16));  // green (locked)
        if (thisPhase == currentPhase)
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));  // blue (active)
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 160));    // grey (future)
    }

    // ── Session commands ──────────────────────────────────────────────────────

    private async void NewSession_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NewSessionCommand.ExecuteAsync(null);
        UpdatePhaseVisuals();
        ScrollConversationToBottom();
    }

    private void RecentSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sessionId })
        {
            ViewModel.LoadExistingSessionCommand.Execute(sessionId);
            UpdatePhaseVisuals();
            ScrollConversationToBottom();
        }
    }

    // ── Conversation commands ─────────────────────────────────────────────────

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        await SendAndScrollAsync();
    }

    private async void MessageInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+Enter sends; Shift+Enter is handled by AcceptsReturn (inserts newline)
        if (e.Key == Windows.System.VirtualKey.Enter &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            await SendAndScrollAsync();
        }
    }

    private async Task SendAndScrollAsync()
    {
        await ViewModel.SendMessageCommand.ExecuteAsync(null);
        ScrollConversationToBottom();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => ViewModel.CancelResponseCommand.Execute(null);

    // ── DSL commands ──────────────────────────────────────────────────────────

    private async void GenerateDsl_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GenerateDslCommand.ExecuteAsync(null);
        DslExpander.IsExpanded = true;
    }

    private async void LockDsl_Click(object sender, RoutedEventArgs e)
    {
        // Confirmation dialog
        var dialog = new ContentDialog
        {
            Title           = "Lock Phase DSL",
            Content         = "Once locked, this phase cannot be edited. You will move to the next phase. Continue?",
            PrimaryButtonText   = "Lock and Proceed",
            CloseButtonText = "Cancel",
            XamlRoot        = XamlRoot,
            DefaultButton   = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var error = await ViewModel.LockPhaseAsync();
        if (error != null)
        {
            var errDialog = new ContentDialog
            {
                Title           = "Lock Failed",
                Content         = error,
                CloseButtonText = "OK",
                XamlRoot        = XamlRoot,
            };
            await errDialog.ShowAsync();
            return;
        }

        ViewModel.AdvancePhase();
        UpdatePhaseVisuals();
        ScrollConversationToBottom();
    }

    private async void DiscardDraft_Click(object sender, RoutedEventArgs e)
        => await ViewModel.DiscardDraftCommand.ExecuteAsync(null);

    private async void ExportDsl_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.DslContent)) return;

        var picker = new FileSavePicker();
        var hwnd   = WindowNative.GetWindowHandle(App.Services.GetRequiredService<MainWindow>());
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("YAML files", [".yaml"]);
        picker.SuggestedFileName = ViewModel.CurrentPhase switch
        {
            SessionPhase.Phase1BusinessDiscovery     => "business",
            SessionPhase.Phase2SolutionShaping       => "solution",
            SessionPhase.Phase3PlanningDecomposition => "plan",
            _ => "dsl",
        };

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            await FileIO.WriteTextAsync(file, ViewModel.DslContent);
        }
        catch (Exception ex)
        {
            var errDialog = new ContentDialog
            {
                Title           = "Export Failed",
                Content         = ex.Message,
                CloseButtonText = "OK",
                XamlRoot        = XamlRoot,
            };
            await errDialog.ShowAsync();
        }
    }

    // ── Phase navigation (read-only review) ───────────────────────────────────

    private async void Phase1Review_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Phase1Locked || ViewModel.CurrentPhase == SessionPhase.Phase1BusinessDiscovery)
            return;
        ViewModel.OpenPhaseReview(SessionPhase.Phase1BusinessDiscovery);
        await ShowPhaseReviewDialogAsync();
    }

    private async void Phase2Review_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Phase2Locked || ViewModel.CurrentPhase == SessionPhase.Phase2SolutionShaping)
            return;
        ViewModel.OpenPhaseReview(SessionPhase.Phase2SolutionShaping);
        await ShowPhaseReviewDialogAsync();
    }

    private async Task ShowPhaseReviewDialogAsync()
    {
        var dslBlock = new TextBlock
        {
            Text                   = ViewModel.ReviewDslContent,
            FontFamily             = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize               = 12,
            Foreground             = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                         Windows.UI.Color.FromArgb(255, 204, 204, 204)),
            TextWrapping           = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var dslBorder = new Border
        {
            Background   = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                               Windows.UI.Color.FromArgb(255, 30, 30, 30)),
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(12),
            Child        = dslBlock,
        };

        var convBlock = new TextBlock
        {
            Text                   = ViewModel.ReviewConversation,
            TextWrapping           = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Conversation", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(convBlock);
        panel.Children.Add(new TextBlock { Text = "Locked DSL",   FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(dslBorder);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollMode  = ScrollMode.Auto,
            MaxHeight            = 500,
            Content              = panel,
        };

        var dialog = new ContentDialog
        {
            Title           = ViewModel.ReviewPhaseTitle,
            Content         = scrollViewer,
            CloseButtonText = "Close",
            XamlRoot        = XamlRoot,
        };

        await dialog.ShowAsync();
        ViewModel.ClosePhaseReview();
    }

    private async void ProceedPhase_Click(object sender, RoutedEventArgs e)
        => await LockDsl_ClickAsync();

    private async Task LockDsl_ClickAsync()
    {
        // Reuse lock logic (same handler as LockDsl_Click)
        var dialog = new ContentDialog
        {
            Title           = "Lock Phase DSL",
            Content         = "Once locked, this phase cannot be edited. You will move to the next phase. Continue?",
            PrimaryButtonText   = "Lock and Proceed",
            CloseButtonText = "Cancel",
            XamlRoot        = XamlRoot,
            DefaultButton   = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var error = await ViewModel.LockPhaseAsync();
        if (error != null)
        {
            var errDialog = new ContentDialog
            {
                Title           = "Lock Failed",
                Content         = error,
                CloseButtonText = "OK",
                XamlRoot        = XamlRoot,
            };
            await errDialog.ShowAsync();
            return;
        }

        ViewModel.AdvancePhase();
        UpdatePhaseVisuals();
        ScrollConversationToBottom();
    }

    // ── Banner dismissal ──────────────────────────────────────────────────────

    private void DismissError_Click(object sender, RoutedEventArgs e)
        => ViewModel.ErrorMessage = null;

    private void DismissSoftWarning_Click(object sender, RoutedEventArgs e)
        => ViewModel.ShowSoftWarning = false;

    // ── Scroll helper ─────────────────────────────────────────────────────────

    private void ScrollConversationToBottom()
    {
        ConversationScrollViewer.UpdateLayout();
        ConversationScrollViewer.ScrollToVerticalOffset(ConversationScrollViewer.ScrollableHeight);
    }
}
