using AiDev.WinUI.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Dialogs;

/// <summary>
/// Modal dialog for creating a new project with name, slug, description,
/// codebase path, and agent template selection — matching the web app's NewProjectDialog.
/// </summary>
public sealed partial class NewProjectDialog : ContentDialog
{
    private readonly ProjectsViewModel _viewModel;
    private bool _slugManuallyEdited;

    private readonly TextBox _nameBox;
    private readonly TextBox _slugBox;
    private readonly TextBox _descriptionBox;
    private readonly TextBox _codebasePathBox;
    private readonly TextBlock _errorText;
    private readonly List<TemplateItem> _templateItems = [];

    public NewProjectDialog(ProjectsViewModel viewModel)
    {
        _viewModel = viewModel;

        Title = "New Project";
        PrimaryButtonText = "Create project";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _nameBox = new TextBox { PlaceholderText = "My Project" };
        _slugBox = new TextBox { PlaceholderText = "my-project", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code,Consolas,monospace") };
        _descriptionBox = new TextBox { PlaceholderText = "What is this project about?" };
        _codebasePathBox = new TextBox { PlaceholderText = @"C:\path\to\your\code", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code,Consolas,monospace") };
        _errorText = new TextBlock { Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red) };

        _nameBox.TextChanged += NameBox_TextChanged;
        _slugBox.TextChanged += SlugBox_TextChanged;

        var panel = new StackPanel { Width = 420, Spacing = 12 };

        panel.Children.Add(BuildField("Project name", _nameBox));
        panel.Children.Add(BuildField("Slug", _slugBox,
            footer: "Used as the folder name. Lowercase letters, digits, and hyphens only."));
        panel.Children.Add(BuildField("Description", _descriptionBox, optional: true));
        panel.Children.Add(BuildField("Codebase path", _codebasePathBox, optional: true,
            footer: "Absolute path to the codebase agents will work on."));

        var templates = _viewModel.GetTemplates();
        if (templates.Count > 0)
        {
            _templateItems.AddRange(templates.Select(t => new TemplateItem(t)));
            panel.Children.Add(BuildTemplatesSection());
        }

        panel.Children.Add(_errorText);

        Content = new ScrollViewer { MaxHeight = 520, Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private static StackPanel BuildField(string label, Control input, bool optional = false, string? footer = null)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        header.Children.Add(new TextBlock { Text = label, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        if (optional)
            header.Children.Add(new TextBlock { Text = "(optional)", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Opacity = 0.6 });

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(header);
        panel.Children.Add(input);
        if (footer != null)
            panel.Children.Add(new TextBlock { Text = footer, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Opacity = 0.6 });
        return panel;
    }

    private UIElement BuildTemplatesSection()
    {
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        headerPanel.Children.Add(new TextBlock { Text = "Agents", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        headerPanel.Children.Add(new TextBlock { Text = "(deselect any you don't need)", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Opacity = 0.6 });

        var list = new StackPanel { Spacing = 6 };
        foreach (var item in _templateItems)
        {
            var nameText = new TextBlock { Text = item.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] };
            var content = new StackPanel();
            content.Children.Add(nameText);
            if (!string.IsNullOrWhiteSpace(item.Description))
                content.Children.Add(new TextBlock { Text = item.Description, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
            content.Children.Add(new TextBlock { Text = $"{item.Slug} · {item.Model}", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code,Consolas,monospace"), Opacity = 0.5 });

            var cb = new CheckBox { IsChecked = true, Content = content };
            cb.Checked += (_, _) => item.IsSelected = true;
            cb.Unchecked += (_, _) => item.IsSelected = false;

            var border = new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Child = cb
            };
            if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var stroke))
                border.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)stroke;
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var bg))
                border.Background = (Microsoft.UI.Xaml.Media.Brush)bg;
            list.Children.Add(border);
        }

        var scroll = new ScrollViewer { MaxHeight = 200, Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(headerPanel);
        section.Children.Add(scroll);
        return section;
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_slugManuallyEdited)
        {
            _slugBox.TextChanged -= SlugBox_TextChanged;
            _slugBox.Text = DeriveSlug(_nameBox.Text);
            _slugBox.TextChanged += SlugBox_TextChanged;
        }
    }

    private void SlugBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _slugManuallyEdited = true;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var name = _nameBox.Text.Trim();
            var slug = _slugBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name)) { ShowError("Project name is required."); args.Cancel = true; return; }
            if (string.IsNullOrWhiteSpace(slug)) { ShowError("Slug is required."); args.Cancel = true; return; }

            var selectedTemplates = _templateItems
                .Where(t => t.IsSelected)
                .ToDictionary(t => t.Slug, t => t.Name);

            var error = await _viewModel.CreateProjectAsync(
                slug, name,
                _descriptionBox.Text.Trim(),
                _codebasePathBox.Text.Trim() is { Length: > 0 } p ? p : null,
                selectedTemplates);

            if (error != null) { ShowError(error); args.Cancel = true; }
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

    public sealed class TemplateItem(AgentTemplate template)
    {
        public string Slug { get; } = template.Slug?.Value ?? string.Empty;
        public string Name { get; } = template.Name;
        public string Description { get; } = template.Description;
        public string Model { get; } = template.Model;
        public bool IsSelected { get; set; } = true;
    }
}

