namespace AiDev.Executors;

/// <summary>
/// Controls the thinking/reasoning budget for a model that supports extended reasoning.
/// Serialised to/from agent.json and template files as lowercase strings ("off", "low", "medium", "high").
/// Only has effect when the selected model has <see cref="ModelCapabilities.Reasoning"/>.
/// </summary>
public enum ThinkingLevel
{
    Off    = 0,
    Low    = 1,
    Medium = 2,
    High   = 3,
}

public static class ThinkingLevelExtensions
{
    /// <summary>Maximum thinking tokens to request for each level.</summary>
    public static int BudgetTokens(this ThinkingLevel level) => level switch
    {
        ThinkingLevel.Low    =>  1_024,
        ThinkingLevel.Medium =>  4_096,
        ThinkingLevel.High   => 16_384,
        _                    =>      0,
    };

    public static string ToDisplayName(this ThinkingLevel level) => level switch
    {
        ThinkingLevel.Off    => "Off",
        ThinkingLevel.Low    => "Low (1K tokens)",
        ThinkingLevel.Medium => "Medium (4K tokens)",
        ThinkingLevel.High   => "High (16K tokens)",
        _                    => level.ToString(),
    };

    /// <summary>Maps "low"/"medium"/"high" used by reasoning_effort APIs.</summary>
    public static string? ToReasoningEffort(this ThinkingLevel level) => level switch
    {
        ThinkingLevel.Low    => "low",
        ThinkingLevel.Medium => "medium",
        ThinkingLevel.High   => "high",
        _                    => null,
    };

    public static ThinkingLevel Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "low"    => ThinkingLevel.Low,
        "medium" => ThinkingLevel.Medium,
        "high"   => ThinkingLevel.High,
        _        => ThinkingLevel.Off,
    };

    public static string Serialize(this ThinkingLevel level) => level.ToString().ToLowerInvariant();
}
