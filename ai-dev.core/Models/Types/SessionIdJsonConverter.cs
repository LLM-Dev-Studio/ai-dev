namespace AiDev.Models.Types;

internal sealed class SessionIdJsonConverter : JsonConverter<SessionId>
{
    public override SessionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Expected string for SessionId.");
        if (!SessionId.TryParse(s, out var id)) throw new JsonException($"Invalid SessionId: '{s}'.");
        return id;
    }

    public override void Write(Utf8JsonWriter writer, SessionId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
