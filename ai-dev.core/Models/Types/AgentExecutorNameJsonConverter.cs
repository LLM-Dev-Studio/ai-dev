namespace AiDev.Models.Types;

internal sealed class AgentExecutorNameJsonConverter : JsonConverter<AgentExecutorName>
{
    public override AgentExecutorName? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return AgentExecutorName.TryParse(value, out var executor) ? executor : null;
    }

    public override void Write(Utf8JsonWriter writer, AgentExecutorName value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
