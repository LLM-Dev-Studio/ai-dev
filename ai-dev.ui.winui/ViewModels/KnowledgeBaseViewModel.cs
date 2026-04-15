using AiDev.Features.KnowledgeBase;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class KnowledgeBaseViewModel : ObservableObject
{
    private readonly KbService _kbService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial KbArticle? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial string ArticleContent { get; set; } = "";

    [ObservableProperty]
    public partial string NewArticleSlug { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }
    public ObservableCollection<KbArticle> Articles { get; } = [];

    public KnowledgeBaseViewModel(KbService kbService, MainViewModel mainViewModel)
    {
        _kbService = kbService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public Task LoadAsync()
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        IsLoading = true;
        try
        {
            var articles = _kbService.ListArticles(CurrentSlug);
            Articles.Clear();
            foreach (var a in articles) Articles.Add(a);
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task SelectArticleAsync(KbArticle article)
    {
        if (CurrentSlug is null) return Task.CompletedTask;
        SelectedArticle = article;
        ArticleContent = _kbService.GetContent(CurrentSlug, article.Slug);
        HasUnsavedChanges = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task CreateArticleAsync()
    {
        if (CurrentSlug is null || string.IsNullOrWhiteSpace(NewArticleSlug)) return;
        _kbService.Create(CurrentSlug, NewArticleSlug.Trim());
        NewArticleSlug = "";
        await LoadAsync();
    }

    [RelayCommand]
    public void SaveArticle()
    {
        if (CurrentSlug is null || SelectedArticle is null) return;
        IsSaving = true;
        try
        {
            _kbService.Save(CurrentSlug, SelectedArticle.Slug, ArticleContent);
            HasUnsavedChanges = false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public async Task DeleteArticleAsync(KbArticle article)
    {
        if (CurrentSlug is null) return;
        if (SelectedArticle?.Slug == article.Slug)
        {
            SelectedArticle = null;
            ArticleContent = "";
        }
        _kbService.Delete(CurrentSlug, article.Slug);
        await LoadAsync();
    }

    partial void OnArticleContentChanged(string value) => HasUnsavedChanges = SelectedArticle is not null;
}
