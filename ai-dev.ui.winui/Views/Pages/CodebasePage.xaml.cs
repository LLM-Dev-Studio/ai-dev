using AiDev.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AiDev.WinUI.Views.Pages;

public sealed partial class CodebasePage : Page
{
    public CodebaseViewModel ViewModel { get; }

    public CodebasePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CodebaseViewModel>();
        DataContext = ViewModel;
        Loaded += (_, _) =>
        {
            ViewModel.Load();
            CodebaseStatusText.Text = ViewModel.CodebaseInitialized ? "Initialized" : "Not initialized";
            IsGitRepoText.Text = ViewModel.IsGitRepo ? "Yes" : "No";
        };
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CodebaseViewModel.CodebaseInitialized))
                CodebaseStatusText.Text = ViewModel.CodebaseInitialized ? "Initialized" : "Not initialized";
            if (e.PropertyName is nameof(CodebaseViewModel.IsGitRepo))
                IsGitRepoText.Text = ViewModel.IsGitRepo ? "Yes" : "No";
            if (e.PropertyName is nameof(CodebaseViewModel.InitMode))
                LinkRadio.IsChecked = ViewModel.InitMode == "link";
        };
    }

    private void SelectCommit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hash })
            ViewModel.SelectCommitCommand.Execute(hash);
    }

    private void CreateMode_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.InitMode = "create";
    }

    private void LinkMode_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.InitMode = "link";
    }
}
