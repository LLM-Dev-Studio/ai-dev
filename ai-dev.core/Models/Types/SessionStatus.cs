namespace AiDev.Models.Types;

/// <summary>
/// Overall completion status reported by a finished session.
/// </summary>
[JsonConverter(typeof(SessionStatusJsonConverter))]
public readonly record struct SessionStatus
{
    /// <summary>
    /// Represents a completed session.
    /// </summary>
    public static readonly SessionStatus Completed = new("completed");

    /// <summary>
    /// Represents a failed session.
    /// </summary>
    public static readonly SessionStatus Failed = new("failed");

    /// <summary>
    /// Represents a partially completed session.
    /// </summary>
    public static readonly SessionStatus Partial = new("partial");

    /// <summary>
    /// Gets the persisted session status value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a session status from a validated value.
    /// </summary>
    /// <param name="value">The session status value.</param>
    public SessionStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Session status cannot be empty.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a <see cref="SessionStatus"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw session status value.</param>
    /// <returns>The parsed session status.</returns>
    public static SessionStatus From(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "completed" => Completed,
        "failed" => Failed,
        "partial" => Partial,
        _ => throw new ArgumentException("Session status must be one of: completed, failed, partial.", nameof(value)),
    };

    /// <summary>
    /// Gets a value indicating whether the status is completed.
    /// </summary>
    public bool IsCompleted => this == Completed;

    /// <summary>
    /// Gets a value indicating whether the status is failed.
    /// </summary>
    public bool IsFailed => this == Failed;

    /// <summary>
    /// Gets a value indicating whether the status is partial.
    /// </summary>
    public bool IsPartial => this == Partial;

    /// <summary>
    /// Converts the session status to its persisted string value.
    /// </summary>
    /// <param name="sessionStatus">The session status to convert.</param>
    public static implicit operator string(SessionStatus sessionStatus) => sessionStatus.Value;
    /// <summary>
    /// Converts a raw value to a session status.
    /// </summary>
    /// <param name="value">The raw session status value.</param>
    public static implicit operator SessionStatus(string value) => From(value);

    public override string ToString() => Value;
}
