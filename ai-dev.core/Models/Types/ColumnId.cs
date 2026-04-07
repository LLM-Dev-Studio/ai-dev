namespace AiDev.Models.Types;

[JsonConverter(typeof(ColumnIdJsonConverter))]
public sealed partial record ColumnId : IParsable<ColumnId>
{
    public static readonly ColumnId Backlog = new("backlog");
    public static readonly ColumnId InProgress = new("in-progress");
    public static readonly ColumnId Review = new("review");
    public static readonly ColumnId Done = new("done");

    public string Value { get; }

    public ColumnId(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid column ID '{value}'. Must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.",
                nameof(value));
        Value = value;
    }

    public static ColumnId From(string? value)
        => value?.ToLowerInvariant() switch
        {
            "backlog" => Backlog,
            "in-progress" => InProgress,
            "review" => Review,
            "done" => Done,
            _ when !string.IsNullOrWhiteSpace(value) => new(value),
            _ => throw new ArgumentException("Column id is required.", nameof(value)),
        };

    public static bool TryParse(string? value, [NotNullWhen(true)] out ColumnId? columnId)
    {
        if (!IsValid(value))
        {
            columnId = null;
            return false;
        }

        columnId = From(value);
        return true;
    }

    static ColumnId IParsable<ColumnId>.Parse(string s, IFormatProvider? provider) => From(s);

    static bool IParsable<ColumnId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out ColumnId result)
        => TryParse(s, out result);

    public override string ToString() => Value;

    public static implicit operator string(ColumnId columnId) => columnId.Value;
    public static implicit operator ColumnId(string value) => From(value);

    private static bool IsValid([NotNullWhen(true)] string? value)
        => !string.IsNullOrWhiteSpace(value) && ColumnIdPattern().IsMatch(value);

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")]
    private static partial Regex ColumnIdPattern();
}

internal sealed class ColumnIdJsonConverter : JsonConverter<ColumnId>
{
    public override ColumnId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException("Expected string for ColumnId.");
        if (!ColumnId.TryParse(value, out var columnId))
            throw new JsonException($"Invalid ColumnId: '{value}'.");
        return columnId;
    }

    public override void Write(Utf8JsonWriter writer, ColumnId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
