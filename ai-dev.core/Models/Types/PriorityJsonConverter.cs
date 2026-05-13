namespace AiDev.Models.Types;

internal sealed class PriorityJsonConverter : JsonConverter<Priority>
{
    public override Priority Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Priority.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, Priority value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
