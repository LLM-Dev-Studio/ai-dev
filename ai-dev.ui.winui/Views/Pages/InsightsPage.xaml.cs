using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class InsightsPage : Page
{
    public InsightsViewModel ViewModel { get; }

    public InsightsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<InsightsViewModel>();
        DataContext = ViewModel;
        Loaded += (_, _) => ViewModel.Load();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(InsightsViewModel.SelectedInsight))
                RenderInsight();
            if (e.PropertyName is nameof(InsightsViewModel.SelectedDate))
                NoInsightsText.Visibility = ViewModel.SelectedDate is not null && ViewModel.SelectedInsight is null
                    ? Visibility.Visible : Visibility.Collapsed;
        };
    }

    private void AgentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AgentList.SelectedItem is AgentInfo agent)
            ViewModel.SelectAgent(agent);
    }

    private void DateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DateList.SelectedItem is TranscriptDate date)
            ViewModel.SelectDate(date);
    }

    private void RenderInsight()
    {
        var insight = ViewModel.SelectedInsight;
        if (insight is null) return;

        ClassificationText.Text = insight.TaskClassification;
        SizeRatingText.Text = insight.SessionSizeRating;

        // Issues
        IssuesList.Items.Clear();
        IssuesPanel.Visibility = insight.Issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var issue in insight.Issues)
        {
            IssuesList.Items.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Background = ImpactBrush(issue.Impact),
                        Child = new TextBlock { Text = issue.Impact, Style = (Style)Resources["CaptionTextBlockStyle"] }
                    },
                    new TextBlock
                    {
                        Text = issue.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Style = (Style)Resources["CaptionTextBlockStyle"]
                    }
                }
            });
        }

        // Knowledge gaps
        GapsList.Items.Clear();
        GapsPanel.Visibility = insight.KnowledgeGaps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var gap in insight.KnowledgeGaps)
            GapsList.Items.Add(new TextBlock
            {
                Text = $"• {gap}",
                Style = (Style)Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });

        // Prompt suggestion
        PromptPanel.Visibility = !string.IsNullOrWhiteSpace(insight.ImprovedPromptSuggestion)
            ? Visibility.Visible : Visibility.Collapsed;
        PromptText.Text = insight.ImprovedPromptSuggestion;
    }

    private static Microsoft.UI.Xaml.Media.Brush ImpactBrush(string impact) => impact switch
    {
        "high"   => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"],
        "medium" => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"],
        _        => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorAttentionBackgroundBrush"],
    };
}
