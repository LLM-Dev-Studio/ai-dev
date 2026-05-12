namespace AiDev.Models.Types;

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
