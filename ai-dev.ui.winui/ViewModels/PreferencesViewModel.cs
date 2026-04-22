using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.WinUI.ViewModels;

public partial class PreferencesViewModel(FeatureFlagsService featureFlagsService) : ObservableObject
{
    private bool _isLoading;

    [ObservableProperty]
    public partial bool LocalFunctionalityEnabled { get; set; }

    [ObservableProperty]
    public partial bool Saved { get; set; }

    [RelayCommand]
    public Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var flags = featureFlagsService.GetFlags();
            LocalFunctionalityEnabled = flags.LocalFunctionalityEnabled;
        }
        finally
        {
            _isLoading = false;
        }
        Saved = false;
        return Task.CompletedTask;
    }

    partial void OnLocalFunctionalityEnabledChanged(bool value)
    {
        if (_isLoading) return;
        featureFlagsService.SaveFlags(new AppFeatureFlags
        {
            LocalFunctionalityEnabled = value,
        });
        Saved = true;
    }
}
