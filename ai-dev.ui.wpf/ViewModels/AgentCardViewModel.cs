using CommunityToolkit.Mvvm.ComponentModel;

namespace AiDev.Desktop.ViewModels;

/// <summary>Wraps an AgentInfo for dashboard card binding, tracking live run state.</summary>
public partial class AgentCardViewModel : ObservableObject
{
    [ObservableProperty] private AgentInfo _agent;
    [ObservableProperty] private bool _isRunning;

    public AgentCardViewModel(AgentInfo agent, bool isRunning)
    {
        _agent = agent;
        _isRunning = isRunning;
    }
}
