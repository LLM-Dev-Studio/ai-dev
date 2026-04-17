using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class TaskTranscriptPage : Page
{
    public TaskTranscriptViewModel ViewModel { get; }

    public TaskTranscriptPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TaskTranscriptViewModel>();
        DataContext = ViewModel;

        ViewModel.NavigateBack += () =>
        {
            var window = App.Services.GetRequiredService<MainWindow>();
            window.NavigateTo("board");
        };

        Loaded += async (_, _) =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            if (mainVm.PendingTaskId is { } taskId)
                await ViewModel.LoadAsync(taskId);
        };
    }
}
