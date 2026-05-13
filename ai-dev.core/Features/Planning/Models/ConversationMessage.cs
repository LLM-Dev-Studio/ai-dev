namespace AiDev.Features.Planning.Models;

/// <summary>
/// Represents a message in a planning conversation transcript.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>
    /// Gets the message role, such as <c>user</c> or <c>assistant</c>.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Gets the message content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the message was recorded.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether an EC-6 filter blocked the original response and substituted the content.
    /// </summary>
    public bool WasFiltered { get; init; }
}
