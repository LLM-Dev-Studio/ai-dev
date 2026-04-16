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
        Loaded += async (_, _) => await ViewModel.LoadAsync();
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    // "＋ Add" button on a column header — Tag is the ColumnId string
    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string columnId })
            ViewModel.OpenNewTask(columnId);
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

    private void PriorityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (sender is ComboBox { SelectedItem: ComboBoxItem { Tag: string priority } })
            ViewModel.TaskPriority = priority;
    }

    private void AssigneeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (sender is ComboBox { SelectedItem: ComboBoxItem { Tag: string tag } })
            ViewModel.TaskAssignee = tag;
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
