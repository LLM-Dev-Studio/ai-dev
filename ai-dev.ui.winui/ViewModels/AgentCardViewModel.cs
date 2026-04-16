using AiDev.Features.Agent;
using AiDev.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AiDev.WinUI.ViewModels;

/// <summary>Wraps an AgentInfo for dashboard card binding, tracking live run state.</summary>
public partial class AgentCardViewModel : ObservableObject
{
    [ObservableProperty] public partial AgentInfo Agent { get; set; }
    [ObservableProperty] public partial bool IsRunning { get; set; }

    public AgentCardViewModel(AgentInfo agent, bool isRunning)
    {
        Agent = agent;
        IsRunning = isRunning;
    }
}
