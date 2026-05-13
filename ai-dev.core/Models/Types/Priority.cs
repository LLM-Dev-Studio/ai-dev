namespace AiDev.Models.Types;

[JsonConverter(typeof(PriorityJsonConverter))]
/// <summary>
/// Represents the priority assigned to a task, message, or decision.
/// </summary>
public readonly record struct Priority
{
    /// <summary>
    /// Represents low priority.
    /// </summary>
    public static readonly Priority Low = new("low");

    /// <summary>
    /// Represents normal priority.
    /// </summary>
    public static readonly Priority Normal = new("normal");

    /// <summary>
    /// Represents high priority.
    /// </summary>
    public static readonly Priority High = new("high");

    /// <summary>
    /// Represents critical priority.
    /// </summary>
    public static readonly Priority Critical = new("critical");

    /// <summary>
    /// Gets the persisted priority value.
    /// </summary>
    public string Value { get; }

    private Priority(string value) => Value = value;

    /// <summary>
    /// Creates a <see cref="Priority"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw priority value.</param>
    /// <returns>The parsed priority, defaulting to <see cref="Normal"/>.</returns>
    public static Priority From(string? value) => value?.ToLowerInvariant() switch
    {
        "low" => Low,
        "high" => High,
        "critical" => Critical,
        _ => Normal,
    };

    /// <summary>
    /// Gets a value indicating whether the priority is low.
    /// </summary>
    public bool IsLow => this == Low;

    /// <summary>
    /// Gets a value indicating whether the priority is normal.
    /// </summary>
    public bool IsNormal => this == Normal;

    /// <summary>
    /// Gets a value indicating whether the priority is high.
    /// </summary>
    public bool IsHigh => this == High;

    /// <summary>
    /// Gets a value indicating whether the priority is critical.
    /// </summary>
    public bool IsCritical => this == Critical;

    /// <summary>
    /// Gets a value indicating whether the priority should be treated as urgent.
    /// </summary>
    public bool IsUrgent => IsHigh || IsCritical;

    /// <summary>
    /// Gets the display name for the priority.
    /// </summary>
    public string DisplayName => Value switch
    {
        "critical" => "Critical",
        "high" => "High",
        "normal" => "Normal",
        _ => "Low",
    };

    /// <summary>
    /// Gets the color associated with the priority.
    /// </summary>
    public string ColorHex => Value switch
    {
        "critical" => "#EF4444",
        "high" => "#F59E0B",
        "normal" => "#3B82F6",
        _ => "#6B7280",
    };

    public override string ToString() => Value;
}
