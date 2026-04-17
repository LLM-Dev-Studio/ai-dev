using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class DecisionDetailPage : Page
{
    public DecisionDetailViewModel ViewModel { get; }

    public DecisionDetailPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DecisionDetailViewModel>();
        DataContext = ViewModel;

        ViewModel.NavigateBack += () =>
        {
            var window = App.Services.GetRequiredService<MainWindow>();
            window.NavigateTo("decisions");
        };

        Loaded += (_, _) =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            if (mainVm.PendingDecisionId is { } id)
                ViewModel.Load(id);
        };

        Unloaded += (_, _) =>
        {
            ViewModel.Dispose();
            RebuildChatList();
        };

        ViewModel.ChatMessages.CollectionChanged += (_, _) => RebuildChatList();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DecisionDetailViewModel.Decision))
            {
                StatusBadge.Text = ViewModel.Decision?.Status.Value ?? "";
                ChatInputPanel.Visibility = ViewModel.Decision?.Status.IsPending == true
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        };
    }

    private void RebuildChatList()
    {
        ChatList.Items.Clear();
        foreach (var msg in ViewModel.ChatMessages)
        {
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                MaxWidth = 500,
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = msg.IsHuman ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = msg.IsHuman
                    ? (Brush)Application.Current.Resources["AccentButtonBackground"]
                    : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = msg.IsHuman ? "You" : msg.From,
                            Opacity = 0.7,
                            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        },
                        new TextBlock
                        {
                            Text = msg.Content,
                            TextWrapping = TextWrapping.Wrap,
                            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                        },
                    }
                }
            };
            ChatList.Items.Add(bubble);
        }
    }
}
