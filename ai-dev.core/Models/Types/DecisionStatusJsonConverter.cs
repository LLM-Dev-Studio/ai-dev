namespace AiDev.Models.Types;

internal sealed class DecisionStatusJsonConverter : JsonConverter<DecisionStatus>
{
    public override DecisionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DecisionStatus.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, DecisionStatus value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
