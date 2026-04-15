using AiDev.Features.Secrets;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.Desktop.ViewModels;

public partial class SecretEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _value = "";
}

public partial class SecretsViewModel : ObservableObject
{
    private readonly SecretsService _secretsService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _newKey = "";
    [ObservableProperty] private string _newValue = "";

    public ObservableCollection<SecretEntryViewModel> Secrets { get; } = [];

    public SecretsViewModel(SecretsService secretsService, MainViewModel mainViewModel)
    {
        _secretsService = secretsService;
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
            var secrets = await Task.Run(() => _secretsService.LoadDecryptedSecrets(CurrentSlug));
            Secrets.Clear();
            foreach (var kvp in secrets)
                Secrets.Add(new SecretEntryViewModel { Key = kvp.Key, Value = kvp.Value });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void AddSecret()
    {
        if (string.IsNullOrWhiteSpace(NewKey)) return;
        Secrets.Add(new SecretEntryViewModel { Key = NewKey.Trim(), Value = NewValue });
        NewKey = "";
        NewValue = "";
        _ = SaveAsync();
    }

    [RelayCommand]
    public void RemoveSecret(SecretEntryViewModel secret)
    {
        if (CurrentSlug is null) return;
        Secrets.Remove(secret);
        _ = Task.Run(() => _secretsService.DeleteSecret(CurrentSlug, secret.Key));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (CurrentSlug is null) return;
        IsSaving = true;
        try
        {
            await Task.Run(() =>
            {
                foreach (var s in Secrets)
                    _secretsService.SetSecret(CurrentSlug, s.Key, s.Value);
            });
        }
        finally
        {
            IsSaving = false;
        }
    }
}
