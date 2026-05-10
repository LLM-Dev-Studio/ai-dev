namespace AiDev.Features.Decision;

/// <summary>
/// A single message in an interactive decision chat session.
/// Persisted to decisions/chats/{decisionId}.jsonl (one JSON object per line).
/// </summary>
public sealed record DecisionChatMessage(
    string Id,
    DecisionId DecisionId,
    string From,
    bool IsHuman,
    string Content,
    DateTime Timestamp);
