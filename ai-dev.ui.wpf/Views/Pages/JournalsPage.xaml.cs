using AiDev.Desktop.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiDev.Desktop.Views.Pages;

public partial class JournalsPage : Page
{
    private readonly JournalsViewModel _viewModel;

    public JournalsPage(JournalsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void Agent_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: AgentInfo agent })
            await _viewModel.SelectAgentAsync(agent);
    }

    private async void Entry_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: JournalEntry entry })
            await _viewModel.SelectEntryAsync(entry);
    }
}
