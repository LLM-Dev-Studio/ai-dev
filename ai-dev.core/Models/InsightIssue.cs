namespace AiDev.Models;

/// <summary>Represents a single issue or problem encountered during an agent session.</summary>
public record InsightIssue(string Description, string Impact); // Impact: high / medium / low
