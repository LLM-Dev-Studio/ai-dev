namespace AiDev.Models.Types;

[JsonConverter(typeof(DecisionStatusJsonConverter))]
public readonly record struct DecisionStatus
{
    public static readonly DecisionStatus Pending = new("pending");
    public static readonly DecisionStatus Resolved = new("resolved");

    public string Value { get; }

    private DecisionStatus(string value) => Value = value;

    public static DecisionStatus From(string? value) => value?.ToLowerInvariant() switch
    {
        "resolved" => Resolved,
        _ => Pending,
    };

    public bool IsPending => this == Pending;
    public bool IsResolved => this == Resolved;

    public override string ToString() => Value;
}

internal sealed class DecisionStatusJsonConverter : JsonConverter<DecisionStatus>
{
    public override DecisionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DecisionStatus.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, DecisionStatus value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
