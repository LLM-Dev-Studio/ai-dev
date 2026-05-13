namespace AiDev.Models.Types;

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
