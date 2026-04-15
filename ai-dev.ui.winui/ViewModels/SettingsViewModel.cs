using AiDev.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.WinUI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StudioSettingsService _settingsService;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string AnthropicApiKey { get; set; } = "";

    [ObservableProperty]
    public partial string OllamaBaseUrl { get; set; } = "";

    [ObservableProperty]
    public partial string LmStudioBaseUrl { get; set; } = "";

    [ObservableProperty]
    public partial string GitHubToken { get; set; } = "";

    [ObservableProperty]
    public partial string InsightsExecutor { get; set; } = "";

    [ObservableProperty]
    public partial string InsightsModel { get; set; } = "";

    public SettingsViewModel(StudioSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    public Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var settings = _settingsService.GetSettings();
            AnthropicApiKey = settings.AnthropicApiKey ?? "";
            OllamaBaseUrl = settings.OllamaBaseUrl;
            LmStudioBaseUrl = settings.LmStudioBaseUrl;
            GitHubToken = settings.GitHubToken ?? "";
            InsightsExecutor = settings.InsightsExecutor ?? "";
            InsightsModel = settings.InsightsModel ?? "";
            return Task.CompletedTask;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public string SaveNote =>
        "Settings are read from appsettings.json. Edit that file and restart to apply changes.";
}
