using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class ConsistencyViewModel : ObservableObject
{
    private readonly ConsistencyCheckService _consistencyService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial int WarningCount { get; set; }
    [ObservableProperty] public partial int ErrorCount { get; set; }
    [ObservableProperty] public partial bool HasRun { get; set; }

    public ObservableCollection<ConsistencyFinding> Findings { get; } = [];

    public ConsistencyViewModel(ConsistencyCheckService consistencyService, MainViewModel mainViewModel)
    {
        _consistencyService = consistencyService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public async Task RunScanAsync()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var report = await _consistencyService.CheckProjectAsync(CurrentSlug);
            Findings.Clear();
            foreach (var f in report.Findings) Findings.Add(f);
            WarningCount = report.Findings.Count(f => f.Severity == ConsistencySeverity.Warning);
            ErrorCount = report.Findings.Count(f => f.Severity == ConsistencySeverity.Error);
            HasRun = true;
        }
        finally { IsLoading = false; }
    }
}
