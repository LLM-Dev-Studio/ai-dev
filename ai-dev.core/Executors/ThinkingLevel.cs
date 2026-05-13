using System.Text.Json.Serialization;

namespace AiDev.Executors;

/// <summary>
/// Controls the thinking/reasoning budget for a model that supports extended reasoning.
/// Serialised to/from agent.json and template files as lowercase strings ("off", "low", "medium", "high").
/// Only has effect when the selected model has <see cref="ModelCapabilities.Reasoning"/>.
/// The default value (<c>default(ThinkingLevel)</c>) is equivalent to <see cref="Off"/>.
/// </summary>
[JsonConverter(typeof(ThinkingLevelJsonConverter))]
public readonly struct ThinkingLevel : IEquatable<ThinkingLevel>
{
    private readonly string? _key;

    private ThinkingLevel(string key, string? reasoningEffort, int budgetTokens, string displayName)
    {
        _key = key;
        ReasoningEffort = reasoningEffort;
        BudgetTokens = budgetTokens;
        DisplayName = displayName;
    }

    /// <summary>
    /// Disables extended reasoning.
    /// </summary>
    public static readonly ThinkingLevel Off    = new("off",    null,     0,      "Off");

    /// <summary>
    /// Uses a low extended reasoning budget.
    /// </summary>
    public static readonly ThinkingLevel Low    = new("low",    "low",    1_024,  "Low (1K tokens)");

    /// <summary>
    /// Uses a medium extended reasoning budget.
    /// </summary>
    public static readonly ThinkingLevel Medium = new("medium", "medium", 4_096,  "Medium (4K tokens)");

    /// <summary>
    /// Uses a high extended reasoning budget.
    /// </summary>
    public static readonly ThinkingLevel High   = new("high",   "high",   16_384, "High (16K tokens)");

    /// <summary>
    /// Gets all supported thinking levels.
    /// </summary>
    public static IReadOnlyList<ThinkingLevel> All { get; } = [Off, Low, Medium, High];

    /// <summary>
    /// Gets the maximum thinking tokens to request. Zero when <see cref="IsOff"/>.
    /// </summary>
    public int BudgetTokens { get; }

    /// <summary>
    /// Gets the reasoning effort value for reasoning-effort APIs. Null when <see cref="IsOff"/>.
    /// </summary>
    public string? ReasoningEffort { get; }

    /// <summary>
    /// Gets the user-facing display name including token count.
    /// </summary>
    public string DisplayName { get; } = "Off";

    /// <summary>
    /// Gets a value indicating whether extended reasoning is disabled.
    /// </summary>
    public bool IsOff => BudgetTokens == 0;

    /// <summary>
    /// Serializes the thinking level to its persisted key.
    /// </summary>
    /// <returns>The serialized thinking level key.</returns>
    public string Serialize() => _key ?? "off";

    /// <summary>
    /// Parses a persisted thinking level value.
    /// </summary>
    /// <param name="value">The raw thinking level value.</param>
    /// <returns>The parsed thinking level, defaulting to <see cref="Off"/>.</returns>
    public static ThinkingLevel Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "low"    => Low,
        "medium" => Medium,
        "high"   => High,
        _        => Off,
    };

    /// <summary>
    /// Determines whether this thinking level equals another instance.
    /// </summary>
    /// <param name="other">The other thinking level.</param>
    /// <returns><see langword="true"/> when the instances are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ThinkingLevel other) => (_key ?? "off") == (other._key ?? "off");
    public override bool Equals(object? obj) => obj is ThinkingLevel other && Equals(other);
    public override int GetHashCode() => (_key ?? "off").GetHashCode();
    public static bool operator ==(ThinkingLevel left, ThinkingLevel right) => left.Equals(right);
    public static bool operator !=(ThinkingLevel left, ThinkingLevel right) => !left.Equals(right);
    public override string ToString() => Serialize();
}
