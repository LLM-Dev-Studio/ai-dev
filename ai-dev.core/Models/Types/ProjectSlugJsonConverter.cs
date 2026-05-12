namespace AiDev.Models.Types;

internal sealed class ProjectSlugJsonConverter : JsonConverter<ProjectSlug>
{
    public override ProjectSlug? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return ProjectSlug.TryParse(value, out var slug) ? slug : null;
    }

    public override void Write(Utf8JsonWriter writer, ProjectSlug value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
