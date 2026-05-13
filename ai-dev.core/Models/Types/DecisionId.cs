namespace AiDev.Models.Types;

/// <summary>
/// A strongly-typed decision identifier. Corresponds to the decision filename without extension
/// (e.g. "20250510-143022-need-input").
/// </summary>
[JsonConverter(typeof(DecisionIdJsonConverter))]
public sealed record DecisionId
{
    /// <summary>
    /// Gets the validated decision identifier value.
    /// </summary>
    public string Value { get; }

    public DecisionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Decision ID cannot be empty.", nameof(value));
        Value = value.Trim();
    }

    /// <summary>
    /// Attempts to parse a decision identifier.
    /// </summary>
    /// <param name="value">The raw identifier value.</param>
    /// <param name="id">The parsed identifier when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out DecisionId? id)
    {
        if (string.IsNullOrWhiteSpace(value)) { id = null; return false; }
        id = new(value);
        return true;
    }

    public static implicit operator string(DecisionId id) => id?.Value ?? string.Empty;
    public static implicit operator DecisionId(string value) => new(value);

    public override string ToString() => Value;
}
