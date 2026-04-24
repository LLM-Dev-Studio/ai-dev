using AiDev.WinUI.ViewModels;
using AiDev.WinUI.Views.Dialogs;

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
    private async void AddTask_Click(object sender, RoutedEventArgs e)
    {
        string? columnId = null;
        if (sender is Button { Tag: ColumnId col })
            columnId = col.Value;
        else if (sender is Button { Tag: string colText } && !string.IsNullOrWhiteSpace(colText))
            columnId = colText;

        if (columnId is null) return;
        ViewModel.OpenNewTask(columnId);
        await ShowTaskDialogAsync();
    }

    // "Edit" button on a task card — Tag is the BoardTask, parent column Tag is the ColumnId string
    private async void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BoardTask task, Parent: FrameworkElement parent })
            return;

        var columnId = FindColumnId(parent) ?? ViewModel.Columns
            .FirstOrDefault(c => c.Tasks.Contains(task))?.Column.Id.Value ?? "";
        ViewModel.OpenEditTask(task, columnId);
        await ShowTaskDialogAsync();
    }

    private async Task ShowTaskDialogAsync()
    {
        var dialog = new TaskDialog(ViewModel) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

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
