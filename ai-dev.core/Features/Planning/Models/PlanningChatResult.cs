namespace AiDev.Features.Planning.Models;

/// <summary>
/// Outcome of a single planning chat turn.
/// </summary>
public sealed class PlanningChatResult
{
    /// <summary>The assistant's response (or the filter-substitute message if <see cref="WasFiltered"/> is true).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>True when EC-6 blocked the raw response and substituted a rephrasing prompt.</summary>
    public bool WasFiltered { get; init; }

    /// <summary>The keyword that triggered the EC-6 filter, if applicable.</summary>
    public string? FilteredKeyword { get; init; }

    /// <summary>Accumulated conversation turns in this phase after this message pair is appended.</summary>
    public int TurnCount { get; init; }

    /// <summary>True when TurnCount has reached or exceeded the soft-warning threshold (25).</summary>
    public bool IsSoftLimitReached { get; init; }

    /// <summary>True when TurnCount has reached or exceeded the hard limit (40). Input is blocked at this point.</summary>
    public bool IsHardLimitReached { get; init; }

    public string? Error { get; init; }

    public bool IsError => Error != null;
}
