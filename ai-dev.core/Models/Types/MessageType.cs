namespace AiDev.Models.Types;

/// <summary>
/// Classifies a message in an agent inbox. Open-ended — values beyond the known set are preserved as-is.
/// </summary>
[JsonConverter(typeof(MessageTypeJsonConverter))]
public readonly record struct MessageType
{
    public static readonly MessageType TaskAssigned    = new("task-assigned");
    public static readonly MessageType DecisionChat    = new("decision-chat");
    public static readonly MessageType DecisionReply   = new("decision-reply");
    public static readonly MessageType OverwatchNudge  = new("overwatch-nudge");

    public string Value { get; }

    public MessageType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Message type cannot be empty.", nameof(value));
        Value = value.Trim().ToLowerInvariant();
    }

    public static MessageType From(string? value) =>
        string.IsNullOrWhiteSpace(value) ? new("unknown") : new(value);

    public static implicit operator string(MessageType t) => t.Value;
    public static implicit operator MessageType(string value) => new(value);

    public override string ToString() => Value;
}

internal sealed class MessageTypeJsonConverter : JsonConverter<MessageType>
{
    public override MessageType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MessageType.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, MessageType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
