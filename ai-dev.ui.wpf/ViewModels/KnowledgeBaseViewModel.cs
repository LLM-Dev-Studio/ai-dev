using AiDev.Features.KnowledgeBase;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class KnowledgeBaseViewModel : ObservableObject
{
    private readonly KbService _kbService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private KbArticle? _selectedArticle;
    [ObservableProperty] private string _articleContent = "";
    [ObservableProperty] private string _newArticleSlug = "";
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _hasUnsavedChanges;

    public ObservableCollection<KbArticle> Articles { get; } = [];

    public KnowledgeBaseViewModel(KbService kbService, MainViewModel mainViewModel)
    {
        _kbService = kbService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var articles = _kbService.ListArticles(CurrentSlug);
            Articles.Clear();
            foreach (var a in articles) Articles.Add(a);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectArticleAsync(KbArticle article)
    {
        if (CurrentSlug is null) return;
        SelectedArticle = article;
        ArticleContent = _kbService.GetContent(CurrentSlug, article.Slug);
        HasUnsavedChanges = false;
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
    public async Task SaveArticleAsync()
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
