namespace AiDev.Models.Types;

[JsonConverter(typeof(DecisionStatusJsonConverter))]
/// <summary>
/// Represents the lifecycle status of a decision.
/// </summary>
public readonly record struct DecisionStatus
{
    /// <summary>
    /// Represents a pending decision.
    /// </summary>
    public static readonly DecisionStatus Pending = new("pending");

    /// <summary>
    /// Represents a resolved decision.
    /// </summary>
    public static readonly DecisionStatus Resolved = new("resolved");

    /// <summary>
    /// Gets the persisted status value.
    /// </summary>
    public string Value { get; }

    private DecisionStatus(string value) => Value = value;

    /// <summary>
    /// Creates a <see cref="DecisionStatus"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw status value.</param>
    /// <returns>The parsed status, defaulting to <see cref="Pending"/>.</returns>
    public static DecisionStatus From(string? value) => value?.ToLowerInvariant() switch
    {
        "resolved" => Resolved,
        _ => Pending,
    };

    /// <summary>
    /// Gets a value indicating whether the decision is pending.
    /// </summary>
    public bool IsPending => this == Pending;

    /// <summary>
    /// Gets a value indicating whether the decision is resolved.
    /// </summary>
    public bool IsResolved => this == Resolved;

    public override string ToString() => Value;
}
