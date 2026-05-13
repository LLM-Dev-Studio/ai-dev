namespace AiDev.Models.Types;

internal sealed class MessageSourceJsonConverter : JsonConverter<MessageSource>
{
    public override MessageSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MessageSource.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, MessageSource value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
