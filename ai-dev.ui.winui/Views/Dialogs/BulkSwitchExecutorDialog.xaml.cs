using AiDev.Models.Types;
using AiDev.WinUI.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Dialogs;

public sealed partial class BulkSwitchExecutorDialog : ContentDialog
{
    private sealed record ExecutorOption(string Value, string DisplayName);

    private readonly ProjectSettingsViewModel _viewModel;
    private readonly ComboBox _executorCombo;
    private readonly TextBlock _errorText;

    public BulkSwitchExecutorDialog(ProjectSettingsViewModel viewModel)
    {
        _viewModel = viewModel;

        Title = "Switch all agents to executor";
        PrimaryButtonText = "Switch all";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var executorOptions = _viewModel.AvailableExecutors
            .Select(value => AgentExecutorName.TryParse(value, out var executor)
                ? new ExecutorOption(executor.Value, executor.DisplayName)
                : new ExecutorOption(value, value))
            .ToList();

        _executorCombo = new ComboBox
        {
            ItemsSource = executorOptions,
            DisplayMemberPath = nameof(ExecutorOption.DisplayName),
            SelectedValuePath = nameof(ExecutorOption.Value),
            PlaceholderText = "Select executor"
        };

        if (executorOptions.Count > 0)
            _executorCombo.SelectedIndex = 0;

        _errorText = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
        };

        var panel = new StackPanel { Width = 420, Spacing = 10 };
        panel.Children.Add(BuildField("Target executor", _executorCombo));
        panel.Children.Add(new TextBlock
        {
            Text = $"This updates {viewModel.Agents.Count} agent(s) in the current project.",
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        });
        panel.Children.Add(_errorText);

        Content = panel;
        IsPrimaryButtonEnabled = executorOptions.Count > 0 && viewModel.Agents.Count > 0;
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private static StackPanel BuildField(string label, Control input)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        });
        panel.Children.Add(input);
        return panel;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            HideError();

            var targetExecutor = _executorCombo.SelectedValue as string;
            var error = await _viewModel.ApplyBulkSwitchToExecutorAsync(targetExecutor);
            if (error is not null)
            {
                ShowError(error);
                args.Cancel = true;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void HideError()
    {
        _errorText.Text = string.Empty;
        _errorText.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }
}