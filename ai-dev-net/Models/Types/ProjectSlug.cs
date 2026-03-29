namespace AiDevNet.Services;

/// <summary>
/// A validated project slug: lowercase letters, digits, and hyphens only;
/// must start and end with a letter or digit; minimum 2 characters.
/// </summary>
[JsonConverter(typeof(ProjectSlugJsonConverter))]
public sealed partial record ProjectSlug : IParsable<ProjectSlug>
{
    public string Value { get; }

    public ProjectSlug(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid project slug '{value}'. Must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.",
                nameof(value));
        Value = value;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out ProjectSlug? slug)
    {
        if (!IsValid(value)) { slug = null; return false; }
        slug = new(value!);
        return true;
    }

    // IParsable<ProjectSlug> — enables typed Blazor route parameters
    static ProjectSlug IParsable<ProjectSlug>.Parse(string s, IFormatProvider? provider) => new(s);
    static bool IParsable<ProjectSlug>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out ProjectSlug result)
        => TryParse(s, out result);

    public static implicit operator string(ProjectSlug slug) => slug.Value;
    public static implicit operator ProjectSlug(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && SlugPattern().IsMatch(value)
        && !value.Contains("..")
        && !value.Contains('/')
        && !value.Contains('\\');

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")]
    private static partial System.Text.RegularExpressions.Regex SlugPattern();
}

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
