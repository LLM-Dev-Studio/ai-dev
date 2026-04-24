using AiDev.WinUI.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Dialogs;

public sealed partial class NewAgentDialog : ContentDialog
{
    private readonly AgentDashboardViewModel _viewModel;

    private readonly TextBox _nameBox;
    private readonly TextBox _slugBox;
    private readonly ComboBox _templateCombo;
    private readonly TextBlock _errorText;

    public NewAgentDialog(AgentDashboardViewModel viewModel)
    {
        _viewModel = viewModel;

        Title = "Add Agent";
        PrimaryButtonText = "Create agent";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _templateCombo = new ComboBox
        {
            ItemsSource = _viewModel.Templates,
            DisplayMemberPath = "Name",
            PlaceholderText = "Select template"
        };

        if (_viewModel.Templates.Count > 0)
            _templateCombo.SelectedIndex = 0;

        _nameBox = new TextBox { PlaceholderText = "e.g. Alex" };
        _slugBox = new TextBox
        {
            PlaceholderText = "e.g. dev-alex",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code,Consolas,monospace")
        };

        _nameBox.TextChanged += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_slugBox.Text))
                _slugBox.Text = DeriveSlug(_nameBox.Text);
        };

        _errorText = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
        };

        var panel = new StackPanel { Width = 420, Spacing = 10 };
        panel.Children.Add(BuildField("From template", _templateCombo));
        panel.Children.Add(BuildField("Display name", _nameBox));
        panel.Children.Add(BuildField("Agent slug", _slugBox));
        panel.Children.Add(_errorText);

        Content = panel;
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
            _errorText.Visibility = Visibility.Collapsed;

            if (_templateCombo.SelectedItem is not AgentTemplate template)
            {
                ShowError("Template is required.");
                args.Cancel = true;
                return;
            }

            var error = await _viewModel.CreateAgentAsync(template.Slug.Value, _nameBox.Text, _slugBox.Text);
            if (error is not null)
            {
                ShowError(error);
                args.Cancel = true;
                return;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ShowError(string message)
    {
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }

    private static string DeriveSlug(string name) =>
        new string(name.ToLowerInvariant()
            .Replace(' ', '-')
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray())
        .Trim('-');
}
