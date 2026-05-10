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

    public static readonly ThinkingLevel Off    = new("off",    null,     0,      "Off");
    public static readonly ThinkingLevel Low    = new("low",    "low",    1_024,  "Low (1K tokens)");
    public static readonly ThinkingLevel Medium = new("medium", "medium", 4_096,  "Medium (4K tokens)");
    public static readonly ThinkingLevel High   = new("high",   "high",   16_384, "High (16K tokens)");

    public static IReadOnlyList<ThinkingLevel> All { get; } = [Off, Low, Medium, High];

    /// <summary>Maximum thinking tokens to request. Zero when <see cref="IsOff"/>.</summary>
    public int BudgetTokens { get; }

    /// <summary>"low"/"medium"/"high" for reasoning_effort APIs. Null when <see cref="IsOff"/>.</summary>
    public string? ReasoningEffort { get; }

    /// <summary>User-facing display name including token count.</summary>
    public string DisplayName { get; } = "Off";

    public bool IsOff => BudgetTokens == 0;

    public string Serialize() => _key ?? "off";

    public static ThinkingLevel Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "low"    => Low,
        "medium" => Medium,
        "high"   => High,
        _        => Off,
    };

    public bool Equals(ThinkingLevel other) => (_key ?? "off") == (other._key ?? "off");
    public override bool Equals(object? obj) => obj is ThinkingLevel other && Equals(other);
    public override int GetHashCode() => (_key ?? "off").GetHashCode();
    public static bool operator ==(ThinkingLevel left, ThinkingLevel right) => left.Equals(right);
    public static bool operator !=(ThinkingLevel left, ThinkingLevel right) => !left.Equals(right);
    public override string ToString() => Serialize();
}
