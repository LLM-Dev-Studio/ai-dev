using AiDev.Features.Agent;
using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class ProjectSettingsPage : Page
{
    public ProjectSettingsViewModel ViewModel { get; }

    public ProjectSettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ProjectSettingsViewModel>();
        DataContext = ViewModel;

        Loaded += (_, _) =>
        {
            ViewModel.Load();
            TemplateCombo.ItemsSource = ViewModel.Templates;
            TemplateCombo.DisplayMemberPath = "Name";
        };
    }

    private void OpenAgent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AgentInfo agent })
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            mainVm.PendingAgent = agent;
            App.Services.GetRequiredService<MainWindow>().NavigateTo("detail");
        }
    }

    private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is AgentTemplate template)
        {
            ViewModel.NewAgentTemplate = template.Slug.Value;
            if (string.IsNullOrWhiteSpace(ViewModel.NewAgentSlug))
                ViewModel.NewAgentSlug = template.Slug.Value;
            if (string.IsNullOrWhiteSpace(ViewModel.NewAgentName))
                ViewModel.NewAgentName = template.Name;
        }
    }
}
