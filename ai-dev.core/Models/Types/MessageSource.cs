namespace AiDev.Models.Types;

/// <summary>
/// Identifies the origin of a message. Open-ended — agent slugs are valid sources too.
/// </summary>
[JsonConverter(typeof(MessageSourceJsonConverter))]
public readonly record struct MessageSource
{
    public static readonly MessageSource Board     = new("board");
    public static readonly MessageSource Human     = new("human");
    public static readonly MessageSource Overwatch = new("overwatch");

    public string Value { get; }

    public MessageSource(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Message source cannot be empty.", nameof(value));
        Value = value.Trim();
    }

    public static MessageSource From(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Board : new(value);

    public bool IsBoard     => Value == "board";
    public bool IsHuman     => Value == "human";
    public bool IsOverwatch => Value == "overwatch";

    public static implicit operator string(MessageSource s) => s.Value;
    public static implicit operator MessageSource(string value) => new(value);

    public override string ToString() => Value;
}

internal sealed class MessageSourceJsonConverter : JsonConverter<MessageSource>
{
    public override MessageSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MessageSource.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, MessageSource value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
