using AiDev.Features.Git;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class CodebaseViewModel : ObservableObject
{
    private readonly GitService _gitService;
    private readonly WorkspacePaths _paths;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool CodebaseInitialized { get; set; }
    [ObservableProperty] public partial bool IsGitRepo { get; set; }
    [ObservableProperty] public partial string ProjectDir { get; set; } = "";
    [ObservableProperty] public partial string CodebasePath { get; set; } = "";
    [ObservableProperty] public partial string InitMode { get; set; } = "create";
    [ObservableProperty] public partial string CustomPath { get; set; } = "";
    [ObservableProperty] public partial string InitError { get; set; } = "";
    [ObservableProperty] public partial bool IsInitializing { get; set; }
    [ObservableProperty] public partial bool InitDone { get; set; }
    [ObservableProperty] public partial GitCommitDetail? SelectedCommit { get; set; }

    public bool IsLinkMode => InitMode == "link";

    partial void OnInitModeChanged(string value) => OnPropertyChanged(nameof(IsLinkMode));

    public ObservableCollection<GitCommit> Commits { get; } = [];

    public CodebaseViewModel(GitService gitService, WorkspacePaths paths, MainViewModel mainViewModel)
    {
        _gitService = gitService;
        _paths = paths;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public void Load()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        InitDone = false;
        InitError = "";
        SelectedCommit = null;
        Commits.Clear();
        try
        {
            ProjectDir = _paths.ProjectDir(CurrentSlug).Value;
            var jsonPath = Path.Combine(ProjectDir, "project.json");
            if (File.Exists(jsonPath))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                var root = doc.RootElement;
                CodebaseInitialized = root.TryGetProperty("codebaseInitialized", out var ci) && ci.GetBoolean();
                CodebasePath = root.TryGetProperty("codebasePath", out var cp) ? cp.GetString() ?? "" : "";
            }

            if (CodebaseInitialized && !string.IsNullOrEmpty(CodebasePath))
            {
                IsGitRepo = _gitService.IsGitRepo(CodebasePath);
                if (IsGitRepo)
                    foreach (var c in _gitService.GetLog(CodebasePath)) Commits.Add(c);
            }
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void Initialize()
    {
        if (CurrentSlug is null) return;
        IsInitializing = true;
        InitError = "";
        try
        {
            string cbPath;
            if (InitMode == "create")
            {
                cbPath = Path.Combine(ProjectDir, "codebase");
                Directory.CreateDirectory(cbPath);
            }
            else
            {
                cbPath = CustomPath.Trim();
                if (!Directory.Exists(cbPath)) throw new Exception($"Directory not found: {cbPath}");
            }
            UpdateProjectJson(cbPath);
            CodebasePath = cbPath;
            CodebaseInitialized = true;
            InitDone = true;
            IsGitRepo = _gitService.IsGitRepo(cbPath);
            Commits.Clear();
            if (IsGitRepo)
                foreach (var c in _gitService.GetLog(cbPath)) Commits.Add(c);
        }
        catch (Exception ex) { InitError = ex.Message; }
        finally { IsInitializing = false; }
    }

    [RelayCommand]
    public void SelectCommit(string hash)
        => SelectedCommit = _gitService.GetCommit(CodebasePath, hash);

    [RelayCommand]
    public void CloseDetail() => SelectedCommit = null;

    private void UpdateProjectJson(string cbPath)
    {
        var path = Path.Combine(ProjectDir, "project.json");
        var json = File.Exists(path) ? File.ReadAllText(path) : "{}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var dict = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            dict[prop.Name] = (object?)prop.Value;
        dict["codebaseInitialized"] = true;
        dict["codebasePath"] = cbPath;
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(dict,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
