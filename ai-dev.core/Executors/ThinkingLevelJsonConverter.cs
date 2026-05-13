namespace AiDev.Executors;

internal sealed class ThinkingLevelJsonConverter : JsonConverter<ThinkingLevel>
{
    public override ThinkingLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            // Legacy: numeric values stored before string serialisation was introduced (0=Off,1=Low,2=Medium,3=High)
            var index = reader.GetInt32();
            return index switch { 1 => ThinkingLevel.Low, 2 => ThinkingLevel.Medium, 3 => ThinkingLevel.High, _ => ThinkingLevel.Off };
        }
        return ThinkingLevel.Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, ThinkingLevel value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Serialize());
}
