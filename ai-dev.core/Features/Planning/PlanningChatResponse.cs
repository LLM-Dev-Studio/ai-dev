namespace AiDev.Features.Planning;

/// <summary>
/// The result of a single LLM turn in a planning conversation.
/// </summary>
public sealed record PlanningChatResponse(
    /// <summary>The assistant's response content, or a filter-blocked message if <see cref="WasFiltered"/> is true.</summary>
    string Content,

    /// <summary>True when the EC-6 filter blocked the original response.</summary>
    bool WasFiltered,

    /// <summary>The specific terms that triggered the EC-6 filter (empty when not filtered).</summary>
    IReadOnlyList<string> FilteredTerms,

    /// <summary>Input token count reported by the API for this request (includes all history + system prompt).</summary>
    int InputTokens,

    /// <summary>Output token count reported by the API for this request.</summary>
    int OutputTokens,

    /// <summary>Token status after this response, used to trigger soft warning or hard limit UI.</summary>
    TokenStatus TokenStatus);
