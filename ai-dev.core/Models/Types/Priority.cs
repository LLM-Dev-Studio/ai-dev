namespace AiDev.Models.Types;

[JsonConverter(typeof(PriorityJsonConverter))]
public readonly record struct Priority
{
    public static readonly Priority Low = new("low");
    public static readonly Priority Normal = new("normal");
    public static readonly Priority High = new("high");
    public static readonly Priority Critical = new("critical");

    public string Value { get; }

    private Priority(string value) => Value = value;

    public static Priority From(string? value) => value?.ToLowerInvariant() switch
    {
        "low" => Low,
        "high" => High,
        "critical" => Critical,
        _ => Normal,
    };

    public bool IsLow => this == Low;
    public bool IsNormal => this == Normal;
    public bool IsHigh => this == High;
    public bool IsCritical => this == Critical;
    public bool IsUrgent => IsHigh || IsCritical;

    public override string ToString() => Value;
}

internal sealed class PriorityJsonConverter : JsonConverter<Priority>
{
    public override Priority Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Priority.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, Priority value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
