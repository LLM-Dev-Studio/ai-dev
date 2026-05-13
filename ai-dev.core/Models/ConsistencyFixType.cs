namespace AiDev.Models;

/// <summary>
/// Represents how a consistency issue can be addressed.
/// </summary>
public enum ConsistencyFixType
{
    /// <summary>
    /// No fix was required or available.
    /// </summary>
    None,

    /// <summary>
    /// The issue was repaired automatically.
    /// </summary>
    AutoRepaired,

    /// <summary>
    /// The issue requires manual action.
    /// </summary>
    ManualActionRequired,
}
