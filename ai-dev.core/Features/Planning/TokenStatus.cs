namespace AiDev.Features.Planning;

/// <summary>
/// Represents the warning level derived from planning token usage.
/// </summary>
public enum TokenWarningLevel
{
    /// <summary>
    /// No warning threshold has been reached.
    /// </summary>
    None,

    /// <summary>
    /// Soft warning threshold reached (32,000 tokens). User should consider generating DSL soon.
    /// </summary>
    Soft,

    /// <summary>
    /// Hard limit reached (40,000 tokens). Further input is blocked.
    /// </summary>
    Hard,
}

/// <summary>
/// Represents the current token usage status for a phase conversation.
/// </summary>
/// <param name="TotalTokens">The total input tokens currently used.</param>
/// <param name="WarningLevel">The warning level derived from the current token count.</param>
public sealed record TokenStatus(int TotalTokens, TokenWarningLevel WarningLevel)
{
    /// <summary>Soft warning threshold: 32,000 input tokens.</summary>
    public const int SoftWarningThreshold = 32_000;

    /// <summary>Hard limit threshold: 40,000 input tokens. Input is blocked beyond this.</summary>
    public const int HardLimitThreshold = 40_000;

    /// <summary>
    /// Gets a value indicating whether the hard token limit has been reached.
    /// </summary>
    public bool IsAtHardLimit => WarningLevel == TokenWarningLevel.Hard;

    /// <summary>
    /// Gets a value indicating whether any warning threshold has been reached.
    /// </summary>
    public bool IsWarning => WarningLevel != TokenWarningLevel.None;

    /// <summary>Derives a <see cref="TokenStatus"/> from the raw token count.</summary>
    public static TokenStatus From(int totalTokens) => new(
        totalTokens,
        totalTokens >= HardLimitThreshold ? TokenWarningLevel.Hard :
        totalTokens >= SoftWarningThreshold ? TokenWarningLevel.Soft :
        TokenWarningLevel.None);
}
