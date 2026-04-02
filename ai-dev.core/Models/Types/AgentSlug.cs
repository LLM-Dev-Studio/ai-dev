namespace AiDev.Models.Types;

/// <summary>
/// A validated agent slug: lowercase letters, digits, and hyphens only;
/// must start and end with a letter or digit; minimum 2 characters.
/// </summary>
[JsonConverter(typeof(AgentSlugJsonConverter))]
public sealed partial record AgentSlug : IParsable<AgentSlug>
{
    public string Value { get; }

    public AgentSlug(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid agent slug '{value}'. Must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.",
                nameof(value));
        Value = value;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out AgentSlug? slug)
    {
        if (!IsValid(value)) { slug = null; return false; }
        slug = new(value!);
        return true;
    }

    // IParsable<AgentSlug> — enables typed Blazor route parameters
    static AgentSlug IParsable<AgentSlug>.Parse(string s, IFormatProvider? provider) => new(s);
    static bool IParsable<AgentSlug>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out AgentSlug result)
        => TryParse(s, out result);

    public static implicit operator string(AgentSlug slug) => slug.Value;
    public static implicit operator AgentSlug(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && SlugPattern().IsMatch(value)
        && !value.Contains("..")
        && !value.Contains('/')
        && !value.Contains('\\');

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")]
    private static partial Regex SlugPattern();
}

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
