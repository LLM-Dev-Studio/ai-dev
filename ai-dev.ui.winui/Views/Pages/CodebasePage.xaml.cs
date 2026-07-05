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
            IsGitRepoText.Text = ViewModel.IsGitRepo ? "Yes" : "No";
        };
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CodebaseViewModel.IsGitRepo))
                IsGitRepoText.Text = ViewModel.IsGitRepo ? "Yes" : "No";
        };
    }

    private void SelectCommit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hash })
            ViewModel.SelectCommitCommand.Execute(hash);
    }
}
