namespace AiDev.Models.Types;

/// <summary>
/// Classifies a message in an agent inbox. Open-ended — values beyond the known set are preserved as-is.
/// </summary>
[JsonConverter(typeof(MessageTypeJsonConverter))]
public readonly record struct MessageType
{
    /// <summary>
    /// Represents a task assignment message.
    /// </summary>
    public static readonly MessageType TaskAssigned = new("task-assigned");

    /// <summary>
    /// Represents a decision chat message.
    /// </summary>
    public static readonly MessageType DecisionChat = new("decision-chat");

    /// <summary>
    /// Represents a decision reply message.
    /// </summary>
    public static readonly MessageType DecisionReply = new("decision-reply");

    /// <summary>
    /// Represents an overwatch nudge message.
    /// </summary>
    public static readonly MessageType OverwatchNudge = new("overwatch-nudge");

    /// <summary>
    /// Gets the persisted message type value.
    /// </summary>
    public string Value { get; }

    public MessageType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Message type cannot be empty.", nameof(value));
        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a <see cref="MessageType"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw message type value.</param>
    /// <returns>The parsed message type, defaulting to <c>unknown</c>.</returns>
    public static MessageType From(string? value) =>
        string.IsNullOrWhiteSpace(value) ? new("unknown") : new(value);

    public static implicit operator string(MessageType t) => t.Value;
    public static implicit operator MessageType(string value) => new(value);

    public override string ToString() => Value;
}
