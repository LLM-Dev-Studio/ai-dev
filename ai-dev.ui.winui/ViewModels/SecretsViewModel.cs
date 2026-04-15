using AiDev.Features.Secrets;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class SecretsViewModel : ObservableObject
{
    private readonly SecretsService _secretsService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string NewKey { get; set; } = "";

    [ObservableProperty]
    public partial string NewValue { get; set; } = "";

    /// <summary>Name of the secret pending delete confirmation; null when no delete is in progress.</summary>
    [ObservableProperty]
    public partial string? PendingDeleteKey { get; set; }

    /// <summary>Secret names only — values are never exposed in the UI.</summary>
    public ObservableCollection<string> SecretNames { get; } = [];

    public SecretsViewModel(SecretsService secretsService, MainViewModel mainViewModel)
    {
        _secretsService = secretsService;
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
            var names = _secretsService.ListSecrets(CurrentSlug);
            SecretNames.Clear();
            foreach (var n in names) SecretNames.Add(n);
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddSecretAsync()
    {
        if (CurrentSlug is null || string.IsNullOrWhiteSpace(NewKey)) return;
        IsSaving = true;
        try
        {
            _secretsService.SetSecret(CurrentSlug, NewKey.Trim(), NewValue);
            NewKey = "";
            NewValue = "";
            await LoadAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>First call — sets PendingDeleteKey to request confirmation.</summary>
    [RelayCommand]
    public void RequestDelete(string name) => PendingDeleteKey = name;

    [RelayCommand]
    public void CancelDelete() => PendingDeleteKey = null;

    /// <summary>Confirmed delete — removes the secret and reloads.</summary>
    [RelayCommand]
    public async Task ConfirmDeleteAsync()
    {
        if (CurrentSlug is null || PendingDeleteKey is null) return;
        _secretsService.DeleteSecret(CurrentSlug, PendingDeleteKey);
        PendingDeleteKey = null;
        await LoadAsync();
    }
}
