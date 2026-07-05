namespace AiDev.Models.Types;

[JsonConverter(typeof(ColumnIdJsonConverter))]
/// <summary>
/// Represents a validated board column identifier.
/// </summary>
public sealed partial record ColumnId : IParsable<ColumnId>
{
    /// <summary>
    /// The backlog column identifier.
    /// </summary>
    public static readonly ColumnId Backlog = new("backlog");

    /// <summary>
    /// The in-progress column identifier.
    /// </summary>
    public static readonly ColumnId InProgress = new("in-progress");

    /// <summary>
    /// The review column identifier.
    /// </summary>
    public static readonly ColumnId Review = new("review");

    /// <summary>
    /// The done column identifier.
    /// </summary>
    public static readonly ColumnId Done = new("done");

    /// <summary>
    /// Gets the validated column identifier value.
    /// </summary>
    public string Value { get; }

    public ColumnId(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid column ID '{value}'. Must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen.",
                nameof(value));
        Value = value;
    }

    /// <summary>
    /// Creates a <see cref="ColumnId"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw column identifier value.</param>
    /// <returns>The parsed column identifier.</returns>
    public static ColumnId From(string? value)
        => value?.ToLowerInvariant() switch
        {
            "backlog" => Backlog,
            "in-progress" => InProgress,
            "review" => Review,
            "done" => Done,
            _ when !string.IsNullOrWhiteSpace(value) => new(value),
            _ => throw new ArgumentException("Column id is required.", nameof(value)),
        };

    /// <summary>
    /// Attempts to parse a validated column identifier.
    /// </summary>
    /// <param name="value">The raw column identifier value.</param>
    /// <param name="columnId">The parsed identifier when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out ColumnId? columnId)
    {
        if (!IsValid(value))
        {
            columnId = null;
            return false;
        }

        columnId = From(value);
        return true;
    }

    static ColumnId IParsable<ColumnId>.Parse(string s, IFormatProvider? provider) => From(s);

    static bool IParsable<ColumnId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out ColumnId result)
        => TryParse(s, out result);

    public override string ToString() => Value;

    public static implicit operator string(ColumnId columnId) => columnId.Value;
    public static implicit operator ColumnId(string value) => From(value);

    private static bool IsValid([NotNullWhen(true)] string? value)
        => !string.IsNullOrWhiteSpace(value) && ColumnIdPattern().IsMatch(value);

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-]*[a-z0-9]$")]
    private static partial Regex ColumnIdPattern();
}
