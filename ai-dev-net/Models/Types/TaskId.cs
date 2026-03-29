namespace AiDevNet.Services;

/// <summary>
/// A strongly-typed task identifier with the format: task-{unix_ms}-{5_hex_chars}.
/// </summary>
[JsonConverter(typeof(TaskIdJsonConverter))]
public sealed partial record TaskId : IParsable<TaskId>
{
    public string Value { get; }

    public TaskId(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid task ID '{value}'. Expected format: task-{{unix_ms}}-{{5 hex chars}}.",
                nameof(value));
        Value = value;
    }

    /// <summary>Generates a new unique task ID.</summary>
    public static TaskId New() =>
        new($"task-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString("N")[..5]}");

    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out TaskId? id)
    {
        if (!IsValid(value)) { id = null; return false; }
        id = new(value!);
        return true;
    }

    // IParsable<TaskId>
    static TaskId IParsable<TaskId>.Parse(string s, IFormatProvider? provider) => new(s);
    static bool IParsable<TaskId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TaskId result)
        => TryParse(s, out result);

    public static implicit operator string(TaskId id) => id.Value;
    public static implicit operator TaskId(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && TaskIdPattern().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex(@"^task-\d+-[a-f0-9]{5}$")]
    private static partial System.Text.RegularExpressions.Regex TaskIdPattern();
}

internal sealed class TaskIdJsonConverter : JsonConverter<TaskId>
{
    public override TaskId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Expected string for TaskId.");
        if (!TaskId.TryParse(s, out var id)) throw new JsonException($"Invalid TaskId: '{s}'.");
        return id;
    }

    public override void Write(Utf8JsonWriter writer, TaskId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    // Required for Dictionary<TaskId, T> key serialisation
    public override TaskId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Read(ref reader, typeToConvert, options);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TaskId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}
