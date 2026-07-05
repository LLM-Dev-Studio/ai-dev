namespace AiDev.Models.Types;

/// <summary>
/// Identifies the origin of a message. Open-ended — agent slugs are valid sources too.
/// </summary>
[JsonConverter(typeof(MessageSourceJsonConverter))]
public readonly record struct MessageSource
{
    /// <summary>
    /// Represents a message originating from the board.
    /// </summary>
    public static readonly MessageSource Board = new("board");

    /// <summary>
    /// Represents a message originating from a human.
    /// </summary>
    public static readonly MessageSource Human = new("human");

    /// <summary>
    /// Represents a message originating from overwatch.
    /// </summary>
    public static readonly MessageSource Overwatch = new("overwatch");

    /// <summary>
    /// Gets the persisted message source value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a message source from a validated value.
    /// </summary>
    /// <param name="value">The message source value.</param>
    public MessageSource(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Message source cannot be empty.", nameof(value));
        Value = value.Trim();
    }

    /// <summary>
    /// Creates a <see cref="MessageSource"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw message source value.</param>
    /// <returns>The parsed message source, defaulting to <see cref="Board"/>.</returns>
    public static MessageSource From(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Board : new(value);

    /// <summary>
    /// Gets a value indicating whether the source is board.
    /// </summary>
    public bool IsBoard => Value == "board";

    /// <summary>
    /// Gets a value indicating whether the source is human.
    /// </summary>
    public bool IsHuman => Value == "human";

    /// <summary>
    /// Gets a value indicating whether the source is overwatch.
    /// </summary>
    public bool IsOverwatch => Value == "overwatch";

    /// <summary>
    /// Converts the message source to its persisted string value.
    /// </summary>
    /// <param name="s">The message source to convert.</param>
    public static implicit operator string(MessageSource s) => s.Value;
    /// <summary>
    /// Converts a raw value to a message source.
    /// </summary>
    /// <param name="value">The raw message source value.</param>
    public static implicit operator MessageSource(string value) => new(value);

    public override string ToString() => Value;
}
