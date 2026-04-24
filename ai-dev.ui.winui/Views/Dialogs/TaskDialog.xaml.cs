using System.ComponentModel;

using AiDev.WinUI.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Dialogs;

public sealed partial class TaskDialog : ContentDialog
{
    private sealed record PriorityOption(string Label, string Value);

    private readonly BoardViewModel _viewModel;
    private readonly TextBox _titleBox;
    private readonly TextBox _descriptionBox;
    private readonly ComboBox _priorityCombo;
    private readonly ComboBox _assigneeCombo;
    private readonly Button _enhanceButton;
    private readonly Button _cancelEnhanceButton;
    private readonly ProgressBar _enhanceProgress;
    private readonly TextBlock _errorText;
    private bool _suppressSync;

    public TaskDialog(BoardViewModel viewModel)
    {
        _viewModel = viewModel;

        Title = viewModel.DialogTitle;
        PrimaryButtonText = viewModel.IsEditing ? "Save changes" : "Create task";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _titleBox = new TextBox { Text = viewModel.TaskTitle, PlaceholderText = "Task title" };
        _descriptionBox = new TextBox
        {
            Text = viewModel.TaskDescription,
            PlaceholderText = "Optional details, acceptance criteria…",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80
        };

        var priorityOptions = new List<PriorityOption>
        {
            new("Low", "low"),
            new("Normal", "normal"),
            new("High", "high"),
            new("Critical", "critical")
        };
        _priorityCombo = new ComboBox
        {
            ItemsSource = priorityOptions,
            DisplayMemberPath = nameof(PriorityOption.Label),
            SelectedValuePath = nameof(PriorityOption.Value),
            SelectedValue = viewModel.TaskPriority,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _assigneeCombo = new ComboBox
        {
            ItemsSource = viewModel.AssigneeOptions,
            DisplayMemberPath = nameof(AssigneeOption.DisplayName),
            SelectedItem = viewModel.SelectedAssigneeOption,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _enhanceButton = new Button { Content = "✨ Enhance" };
        _cancelEnhanceButton = new Button { Content = "Cancel", Visibility = Visibility.Collapsed };
        _enhanceProgress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };

        _errorText = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
        };

        _titleBox.TextChanged += (_, _) => { if (!_suppressSync) _viewModel.TaskTitle = _titleBox.Text; };
        _descriptionBox.TextChanged += (_, _) => { if (!_suppressSync) _viewModel.TaskDescription = _descriptionBox.Text; };
        _priorityCombo.SelectionChanged += (_, _) =>
        {
            if (!_suppressSync && _priorityCombo.SelectedValue is string p)
                _viewModel.TaskPriority = p;
        };
        _assigneeCombo.SelectionChanged += (_, _) =>
        {
            if (!_suppressSync && _assigneeCombo.SelectedItem is AssigneeOption opt)
                _viewModel.SelectedAssigneeOption = opt;
        };
        _enhanceButton.Click += (_, _) => _viewModel.EnhanceCommand.Execute(null);
        _cancelEnhanceButton.Click += (_, _) => _viewModel.CancelEnhanceCommand.Execute(null);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Title row with enhance/cancel buttons side by side
        var titleRow = new Grid { ColumnSpacing = 8 };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_titleBox, 0);
        Grid.SetColumn(_enhanceButton, 1);
        Grid.SetColumn(_cancelEnhanceButton, 1);
        titleRow.Children.Add(_titleBox);
        titleRow.Children.Add(_enhanceButton);
        titleRow.Children.Add(_cancelEnhanceButton);

        var titleField = new StackPanel { Spacing = 4 };
        titleField.Children.Add(BuildLabel("Title"));
        titleField.Children.Add(titleRow);
        titleField.Children.Add(_enhanceProgress);

        // Priority + Assignee side by side
        var metaGrid = new Grid { ColumnSpacing = 12 };
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var priorityField = BuildField("Priority", _priorityCombo);
        var assigneeField = BuildField("Assignee", _assigneeCombo);
        Grid.SetColumn(priorityField, 0);
        Grid.SetColumn(assigneeField, 1);
        metaGrid.Children.Add(priorityField);
        metaGrid.Children.Add(assigneeField);

        var panel = new StackPanel { Width = 500, Spacing = 12 };
        panel.Children.Add(titleField);
        panel.Children.Add(BuildField("Description", _descriptionBox));
        panel.Children.Add(metaGrid);
        panel.Children.Add(_errorText);

        if (viewModel.IsEditing)
        {
            var deleteButton = new Button
            {
                Content = "Delete task",
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
            };
            deleteButton.Click += OnDeleteClick;
            panel.Children.Add(deleteButton);
        }

        Content = panel;
        PrimaryButtonClick += OnPrimaryButtonClick;
        Closed += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private static TextBlock BuildLabel(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
    };

    private static StackPanel BuildField(string label, Control input)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(BuildLabel(label));
        panel.Children.Add(input);
        return panel;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _suppressSync = true;
        try
        {
            switch (e.PropertyName)
            {
                case nameof(BoardViewModel.TaskTitle):
                    if (_titleBox.Text != _viewModel.TaskTitle) _titleBox.Text = _viewModel.TaskTitle;
                    break;
                case nameof(BoardViewModel.TaskDescription):
                    if (_descriptionBox.Text != _viewModel.TaskDescription) _descriptionBox.Text = _viewModel.TaskDescription;
                    break;
                case nameof(BoardViewModel.IsEnhancing):
                    _enhanceButton.Visibility = _viewModel.IsEnhancing ? Visibility.Collapsed : Visibility.Visible;
                    _cancelEnhanceButton.Visibility = _viewModel.IsEnhancing ? Visibility.Visible : Visibility.Collapsed;
                    _enhanceProgress.Visibility = _viewModel.IsEnhancing ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(BoardViewModel.TaskError):
                    if (string.IsNullOrEmpty(_viewModel.TaskError)) HideError();
                    else ShowError(_viewModel.TaskError);
                    break;
            }
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (string.IsNullOrWhiteSpace(_viewModel.TaskTitle))
            {
                ShowError("Title is required.");
                args.Cancel = true;
                return;
            }

            HideError();
            await _viewModel.SaveTaskAsync();

            if (!string.IsNullOrEmpty(_viewModel.TaskError))
                args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.DeleteTaskAsync();
        Hide();
    }

    private void ShowError(string message)
    {
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        _errorText.Text = string.Empty;
        _errorText.Visibility = Visibility.Collapsed;
    }
}
