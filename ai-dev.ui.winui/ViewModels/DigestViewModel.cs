using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AiDev.WinUI.ViewModels;

public partial class DigestViewModel : ObservableObject
{
    private readonly DigestService _digestService;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string DigestDate { get; set; } = "";
    [ObservableProperty] public partial int TotalMessages { get; set; }
    [ObservableProperty] public partial int PendingDecisions { get; set; }
    [ObservableProperty] public partial int ResolvedDecisions { get; set; }

    public ObservableCollection<AgentActivityItem> AgentActivity { get; } = [];

    public DigestViewModel(DigestService digestService, MainViewModel mainViewModel)
    {
        _digestService = digestService;
        _mainViewModel = mainViewModel;
    }

    private ProjectSlug? CurrentSlug => _mainViewModel.ActiveProject?.Slug;

    [RelayCommand]
    public void Load()
    {
        if (CurrentSlug is null) return;
        IsLoading = true;
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var digest = _digestService.GetDigest(CurrentSlug, today);
            DigestDate = digest.Date;
            TotalMessages = digest.TotalMessages;
            PendingDecisions = digest.PendingDecisions;
            ResolvedDecisions = digest.ResolvedDecisions;
            AgentActivity.Clear();
            foreach (var a in digest.AgentActivity) AgentActivity.Add(a);
        }
        finally { IsLoading = false; }
    }
}
