namespace AiDev.Features.Planning.Models;

public sealed class ConversationMessage
{
    /// <summary>"user" or "assistant"</summary>
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }

    /// <summary>True when an EC-6 filter blocked the original response; Content contains the substitute prompt.</summary>
    public bool WasFiltered { get; init; }
}
