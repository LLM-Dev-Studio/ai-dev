using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

/// <summary>
/// Displays the active project's codebase root and its git history.
/// </summary>
public partial class CodebaseViewModel : ObservableObject
{
    private readonly GitService _gitService;
    private readonly ActiveWorkspaceHolder _workspace;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsGitRepo { get; set; }
    [ObservableProperty] public partial string CodebasePath { get; set; } = "";
    [ObservableProperty] public partial GitCommitDetail? SelectedCommit { get; set; }

    public ObservableCollection<GitCommit> Commits { get; } = [];

    public CodebaseViewModel(GitService gitService, ActiveWorkspaceHolder workspace)
    {
        _gitService = gitService;
        _workspace = workspace;
    }

    [RelayCommand]
    public void Load()
    {
        IsLoading = true;
        SelectedCommit = null;
        Commits.Clear();
        try
        {
            CodebasePath = _workspace.ActiveCodebasePath ?? "";
            if (!string.IsNullOrEmpty(CodebasePath))
            {
                IsGitRepo = _gitService.IsGitRepo(CodebasePath);
                if (IsGitRepo)
                    foreach (var c in _gitService.GetLog(CodebasePath)) Commits.Add(c);
            }
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void SelectCommit(string hash)
        => SelectedCommit = _gitService.GetCommit(CodebasePath, hash);

    [RelayCommand]
    public void CloseDetail() => SelectedCommit = null;
}
