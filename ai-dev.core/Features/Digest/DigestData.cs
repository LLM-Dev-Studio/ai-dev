using AiDev.Features.Agent;

namespace AiDev.Features.Digest;

public class DigestData
{
    public required string Date { get; set; }
    public int TotalMessages { get; set; }
    public int PendingDecisions { get; set; }
    public int ResolvedDecisions { get; set; }
    public List<AgentActivityItem> AgentActivity { get; set; } = [];
}
