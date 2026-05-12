namespace AiDev.Models.Types;

internal sealed class SessionStatusJsonConverter : JsonConverter<SessionStatus>
{
    public override SessionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => SessionStatus.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, SessionStatus value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
