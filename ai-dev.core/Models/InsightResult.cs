namespace AiDev.Models;

/// <summary>
/// AI-generated qualitative analysis of a completed agent session.
/// Written as <c>{date}.insights.json</c> alongside the transcript file.
/// </summary>
public record InsightResult(
    TaskClassification TaskClassification,
    SessionSizeRating SessionSizeRating,
    IReadOnlyList<InsightIssue> Issues,
    IReadOnlyList<string> KnowledgeGaps,
    string ImprovedPromptSuggestion);
