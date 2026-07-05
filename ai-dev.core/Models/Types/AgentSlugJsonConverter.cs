namespace AiDev.Models.Types;

internal sealed class AgentSlugJsonConverter : JsonConverter<AgentSlug>
{
    public override AgentSlug? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return AgentSlug.TryParse(value, out var slug) ? slug : null;
    }

    public override void Write(Utf8JsonWriter writer, AgentSlug value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
