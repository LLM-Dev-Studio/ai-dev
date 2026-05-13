namespace AiDev.Models.Types;

internal sealed class MessageTypeJsonConverter : JsonConverter<MessageType>
{
    public override MessageType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => MessageType.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, MessageType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
