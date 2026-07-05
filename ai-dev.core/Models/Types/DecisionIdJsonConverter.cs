namespace AiDev.Models.Types;

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
