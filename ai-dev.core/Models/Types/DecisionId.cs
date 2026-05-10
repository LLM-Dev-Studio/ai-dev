namespace AiDev.Models.Types;

/// <summary>
/// A strongly-typed decision identifier. Corresponds to the decision filename without extension
/// (e.g. "20250510-143022-need-input").
/// </summary>
[JsonConverter(typeof(DecisionIdJsonConverter))]
public sealed record DecisionId
{
    public string Value { get; }

    public DecisionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Decision ID cannot be empty.", nameof(value));
        Value = value.Trim();
    }

    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out DecisionId? id)
    {
        if (string.IsNullOrWhiteSpace(value)) { id = null; return false; }
        id = new(value);
        return true;
    }

    public static implicit operator string(DecisionId id) => id.Value;
    public static implicit operator DecisionId(string value) => new(value);

    public override string ToString() => Value;
}

internal sealed class DecisionIdJsonConverter : JsonConverter<DecisionId>
{
    public override DecisionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Expected string for DecisionId.");
        return new DecisionId(s);
    }

    public override void Write(Utf8JsonWriter writer, DecisionId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
