namespace AiDev.Models.Types;

/// <summary>
/// A validated project slug: lowercase letters, digits, and hyphens only;
/// must start and end with a letter or digit; minimum 2 characters.
/// </summary>
[JsonConverter(typeof(ProjectSlugJsonConverter))]
public sealed partial record ProjectSlug : IParsable<ProjectSlug>
{
    /// <summary>
    /// Gets the validated slug value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a project slug from a validated value.
    /// </summary>
    /// <param name="value">The project slug value.</param>
    public ProjectSlug(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid project slug '{value}'. Must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.",
                nameof(value));
        Value = value;
    }

    /// <summary>
    /// Attempts to parse a validated project slug.
    /// </summary>
    /// <param name="value">The raw slug value.</param>
    /// <param name="slug">The parsed slug when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
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

    public static implicit operator string(ProjectSlug slug) => slug?.Value ?? string.Empty;
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
