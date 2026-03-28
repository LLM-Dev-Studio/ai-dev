namespace AiDevNet.Services;

/// <summary>
/// Represents a currently-running agent session.
/// </summary>
public class RunningSession
{
    public required ProjectSlug ProjectSlug { get; init; }
    public required AgentSlug AgentSlug { get; init; }
    public required string StartedAt { get; init; }
    public int Pid { get; set; }
}
