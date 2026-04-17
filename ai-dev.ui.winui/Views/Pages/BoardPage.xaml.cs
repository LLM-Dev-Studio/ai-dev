using AiDev.WinUI.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class BoardPage : Page
{
    public BoardViewModel ViewModel { get; }

    public BoardPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<BoardViewModel>();
        DataContext = ViewModel;
        Loaded += OnLoadedAsync;
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    // "＋ Add" button on a column header — Tag is the ColumnId
    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ColumnId columnId })
            ViewModel.OpenNewTask(columnId.Value);
        else if (sender is Button { Tag: string columnIdText } && !string.IsNullOrWhiteSpace(columnIdText))
            ViewModel.OpenNewTask(columnIdText);
    }

    // "Edit" button on a task card — Tag is the BoardTask, parent column Tag is the ColumnId string
    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BoardTask task, Parent: FrameworkElement parent })
        {
            // Walk up the visual tree to find the column container whose Tag holds the ColumnId
            var columnId = FindColumnId(parent) ?? ViewModel.Columns
                .FirstOrDefault(c => c.Tasks.Contains(task))?.Column.Id.Value ?? "";
            ViewModel.OpenEditTask(task, columnId);
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
        => ViewModel.DeleteTaskCommand.Execute(null);

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ClearCompletedCommand.CanExecute(null))
            ViewModel.ClearCompletedCommand.Execute(null);
    }

    private void TaskTranscript_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BoardTask task })
            return;

        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        mainVm.PendingTaskId = task.Id;

        var window = App.Services.GetRequiredService<MainWindow>();
        window.NavigateTo("task-transcript");
    }

    // Walk up the visual parent chain looking for a FrameworkElement whose Tag is a column id string
    private static string? FindColumnId(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement fe && fe.Tag is string s && !string.IsNullOrEmpty(s))
                return s;
            element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
