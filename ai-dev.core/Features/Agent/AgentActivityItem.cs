namespace AiDev.Features.Agent;

/// <summary>
/// Represents agent activity details for display and reporting.
/// </summary>
public class AgentActivityItem
{
    /// <summary>
    /// Gets or sets the unique agent slug.
    /// </summary>
    public AgentSlug AgentSlug { get; set; } = new("unnamed");

    /// <summary>
    /// Gets or sets the display name of the agent.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configured executor for the agent.
    /// </summary>
    public AgentExecutorName? Executor { get; set; }

    /// <summary>
    /// Gets or sets the model identifier used by the agent.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of messages sent by the agent.
    /// </summary>
    public int MessagesSent { get; set; }

    /// <summary>
    /// Gets or sets the number of messages received by the agent.
    /// </summary>
    public int MessagesReceived { get; set; }
}
