using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class JournalsPage : Page
{
    public JournalsViewModel ViewModel { get; }

    public JournalsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<JournalsViewModel>();
        DataContext = ViewModel;
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        await JournalWebView.EnsureCoreWebView2Async();
        RenderMarkdown();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void Agent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedAgent is { } agent)
            await ViewModel.SelectAgentAsync(agent);
    }

    private async void Entry_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (ViewModel.SelectedEntry is { } entry)
        {
            await ViewModel.SelectEntryAsync(entry);
            RenderMarkdown();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JournalsViewModel.EntryContentHtml))
            RenderMarkdown();
    }

    private void RenderMarkdown()
    {
        if (JournalWebView.CoreWebView2 is null)
            return;

        JournalWebView.NavigateToString(ViewModel.EntryContentHtml);
    }
}
