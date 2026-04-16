using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class AgentDashboardPage : Page
{
    public AgentDashboardViewModel ViewModel { get; }

    public AgentDashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AgentDashboardViewModel>();
        DataContext = ViewModel;

        ViewModel.AgentSelected += OnAgentSelected;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private void OnAgentSelected(AgentInfo agent)
    {
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        mainVm.PendingAgent = agent;
        if (App.Services.GetService<MainWindow>() is MainWindow w)
            w.NavigateTo("detail");
    }

    private async void RunAgent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AgentCardViewModel card })
            await ViewModel.RunAgentCommand.ExecuteAsync(card);
    }

    private async void StopAgent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AgentCardViewModel card })
            await ViewModel.StopAgentCommand.ExecuteAsync(card);
    }

    private void SelectAgent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AgentCardViewModel card })
            ViewModel.SelectAgentCommand.Execute(card);
    }

    private void AddAgent_Click(object sender, RoutedEventArgs e)
        => ViewModel.IsAddingAgent = true;

    private async void CreateAgent_Click(object sender, RoutedEventArgs e)
        => await ViewModel.CreateAgentCommand.ExecuteAsync(null);

    private void CancelAddAgent_Click(object sender, RoutedEventArgs e)
        => ViewModel.CancelAddAgentCommand.Execute(null);
}
