namespace AiDev.Models.Types;

internal sealed class TaskClassificationJsonConverter : JsonConverter<TaskClassification>
{
    public override TaskClassification Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TaskClassification.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, TaskClassification value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
