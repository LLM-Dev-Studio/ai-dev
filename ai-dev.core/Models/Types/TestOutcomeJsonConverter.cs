namespace AiDev.Models.Types;

internal sealed class TestOutcomeJsonConverter : JsonConverter<TestOutcome>
{
    public override TestOutcome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TestOutcome.From(reader.GetString());

    public override void Write(Utf8JsonWriter writer, TestOutcome value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
