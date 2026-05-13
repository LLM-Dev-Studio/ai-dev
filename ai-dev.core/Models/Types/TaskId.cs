namespace AiDev.Models.Types;

/// <summary>
/// A strongly-typed task identifier with the format: task-{unix_ms}-{5_hex_chars}.
/// </summary>
[JsonConverter(typeof(TaskIdJsonConverter))]
public sealed partial record TaskId : IParsable<TaskId>
{
    /// <summary>
    /// Gets the validated task identifier value.
    /// </summary>
    public string Value { get; }

    public TaskId(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid task ID '{value}'. Expected format: task-{{unix_ms}}-{{5 hex chars}}.",
                nameof(value));
        Value = value;
    }

    /// <summary>Generates a new unique task ID.</summary>
    public static TaskId New() =>
        new($"task-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid().ToString("N")[..5]}");

    /// <summary>
    /// Attempts to parse a validated task identifier.
    /// </summary>
    /// <param name="value">The raw identifier value.</param>
    /// <param name="id">The parsed identifier when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out TaskId? id)
    {
        if (!IsValid(value)) { id = null; return false; }
        id = new(value!);
        return true;
    }

    // IParsable<TaskId>
    static TaskId IParsable<TaskId>.Parse(string s, IFormatProvider? provider) => new(s);
    static bool IParsable<TaskId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TaskId result)
        => TryParse(s, out result);

    public static implicit operator string(TaskId id) => id?.Value ?? string.Empty;
    public static implicit operator TaskId(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && TaskIdPattern().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex(@"^task-\d+-[a-f0-9]{5}$")]
    private static partial System.Text.RegularExpressions.Regex TaskIdPattern();
}
