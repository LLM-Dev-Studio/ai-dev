namespace AiDev.Models;

/// <summary>Represents a single issue or problem encountered during an agent session.</summary>
public record InsightIssue(string Description, string Impact); // Impact: high / medium / low

/// <summary>
/// AI-generated qualitative analysis of a completed agent session.
/// Written as <c>{date}.insights.json</c> alongside the transcript file.
/// </summary>
public record InsightResult(
    string TaskClassification,        // feature / bug / refactor / investigation / other
    string SessionSizeRating,         // small / medium / large
    IReadOnlyList<InsightIssue> Issues,
    IReadOnlyList<string> KnowledgeGaps,
    string ImprovedPromptSuggestion);
