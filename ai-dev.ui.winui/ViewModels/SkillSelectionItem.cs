using CommunityToolkit.Mvvm.ComponentModel;

namespace AiDev.WinUI.ViewModels;

public partial class SkillSelectionItem : ObservableObject
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }
}
