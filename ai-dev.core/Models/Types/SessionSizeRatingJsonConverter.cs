namespace AiDev.Models.Types;

internal sealed class SessionSizeRatingJsonConverter : JsonConverter<SessionSizeRating>
{
    public override SessionSizeRating Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => SessionSizeRating.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, SessionSizeRating value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
