using AiDev.Features.Agent;

namespace AiDev.Features.Digest;

/// <summary>
/// Represents the daily digest data for a project.
/// </summary>
public class DigestData
{
    /// <summary>
    /// Gets or sets the date covered by the digest.
    /// </summary>
    public required DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the total number of messages received for the date.
    /// </summary>
    public int TotalMessages { get; set; }

    /// <summary>
    /// Gets or sets the number of pending decisions.
    /// </summary>
    public int PendingDecisions { get; set; }

    /// <summary>
    /// Gets or sets the number of resolved decisions.
    /// </summary>
    public int ResolvedDecisions { get; set; }

    /// <summary>
    /// Gets or sets the per-agent activity included in the digest.
    /// </summary>
    public List<AgentActivityItem> AgentActivity { get; set; } = [];
}
