namespace AiDev.Models.Types;

/// <summary>
/// A strongly-typed planning session identifier. Format: 32 lowercase hex chars (Guid without dashes).
/// </summary>
[JsonConverter(typeof(SessionIdJsonConverter))]
public sealed partial record SessionId
{
    /// <summary>
    /// Gets the validated session identifier value.
    /// </summary>
    public string Value { get; }

    public SessionId(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException(
                $"Invalid session ID '{value}'. Expected 32 lowercase hex characters.",
                nameof(value));
        Value = value;
    }

    /// <summary>Generates a new unique session ID.</summary>
    public static SessionId New() => new(Guid.CreateVersion7().ToString("N"));

    /// <summary>
    /// Attempts to parse a validated session identifier.
    /// </summary>
    /// <param name="value">The raw identifier value.</param>
    /// <param name="id">The parsed identifier when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out SessionId? id)
    {
        if (!IsValid(value)) { id = null; return false; }
        id = new(value!);
        return true;
    }

    public static implicit operator string(SessionId id) => id.Value;
    public static implicit operator SessionId(string value) => new(value);

    public override string ToString() => Value;

    private static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && SessionIdPattern().IsMatch(value);

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-f0-9]{32}$")]
    private static partial System.Text.RegularExpressions.Regex SessionIdPattern();
}
