namespace AiDevNet.Services;

/// <summary>
/// Represents a currently-running agent session.
/// </summary>
public class RunningSession
{
    public string ProjectSlug { get; set; } = string.Empty;
    public string AgentSlug { get; set; } = string.Empty;
    public int Pid { get; set; }
    public string StartedAt { get; set; } = string.Empty;
}
