namespace AiDev.Executors;

internal sealed class ThinkingLevelJsonConverter : JsonConverter<ThinkingLevel>
{
    public override ThinkingLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ThinkingLevel.Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ThinkingLevel value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Serialize());
}
