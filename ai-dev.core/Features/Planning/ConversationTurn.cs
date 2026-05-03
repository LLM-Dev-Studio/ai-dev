namespace AiDev.Features.Planning;

/// <summary>
/// A single user or assistant message in a planning session conversation.
/// </summary>
public sealed record ConversationTurn(
    ConversationRole Role,
    string Content,
    DateTimeOffset Timestamp,
    SessionPhase Phase,
    int InputTokens = 0,
    int OutputTokens = 0);
