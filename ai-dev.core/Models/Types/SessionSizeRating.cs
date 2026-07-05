namespace AiDev.Models.Types;

/// <summary>
/// Relative size estimate for an analyzed session.
/// </summary>
[JsonConverter(typeof(SessionSizeRatingJsonConverter))]
public readonly record struct SessionSizeRating
{
    /// <summary>
    /// Represents a small session.
    /// </summary>
    public static readonly SessionSizeRating Small = new("small");

    /// <summary>
    /// Represents a medium session.
    /// </summary>
    public static readonly SessionSizeRating Medium = new("medium");

    /// <summary>
    /// Represents a large session.
    /// </summary>
    public static readonly SessionSizeRating Large = new("large");

    /// <summary>
    /// Gets the persisted size rating value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a session size rating from a validated value.
    /// </summary>
    /// <param name="value">The size rating value.</param>
    public SessionSizeRating(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Session size rating cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a <see cref="SessionSizeRating"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw size rating value.</param>
    /// <returns>The parsed size rating, defaulting to <see cref="Medium"/>.</returns>
    public static SessionSizeRating From(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "small" => Small,
        "large" => Large,
        _ => Medium,
    };

    /// <summary>
    /// Gets a value indicating whether the rating is small.
    /// </summary>
    public bool IsSmall => this == Small;

    /// <summary>
    /// Gets a value indicating whether the rating is medium.
    /// </summary>
    public bool IsMedium => this == Medium;

    /// <summary>
    /// Gets a value indicating whether the rating is large.
    /// </summary>
    public bool IsLarge => this == Large;

    public static implicit operator string(SessionSizeRating sizeRating) => sizeRating.Value;
    public static implicit operator SessionSizeRating(string value) => From(value);

    public override string ToString() => Value;
}
