namespace AiDev.Features.Planning;

public enum TokenWarningLevel
{
    None,
    /// <summary>Soft warning threshold reached (32,000 tokens). User should consider generating DSL soon.</summary>
    Soft,
    /// <summary>Hard limit reached (40,000 tokens). Further input is blocked.</summary>
    Hard,
}

/// <summary>
/// Represents the current token usage status for a phase conversation.
/// </summary>
public sealed record TokenStatus(int TotalTokens, TokenWarningLevel WarningLevel)
{
    /// <summary>Soft warning threshold: 32,000 input tokens.</summary>
    public const int SoftWarningThreshold = 32_000;

    /// <summary>Hard limit threshold: 40,000 input tokens. Input is blocked beyond this.</summary>
    public const int HardLimitThreshold = 40_000;

    public bool IsAtHardLimit => WarningLevel == TokenWarningLevel.Hard;
    public bool IsWarning     => WarningLevel != TokenWarningLevel.None;

    /// <summary>Derives a <see cref="TokenStatus"/> from the raw token count.</summary>
    public static TokenStatus From(int totalTokens) => new(
        totalTokens,
        totalTokens >= HardLimitThreshold ? TokenWarningLevel.Hard :
        totalTokens >= SoftWarningThreshold ? TokenWarningLevel.Soft :
        TokenWarningLevel.None);
}
