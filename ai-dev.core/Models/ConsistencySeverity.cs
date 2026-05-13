namespace AiDev.Models;

/// <summary>
/// Represents the severity of a consistency finding.
/// </summary>
public enum ConsistencySeverity
{
    /// <summary>
    /// Informational finding.
    /// </summary>
    Info,

    /// <summary>
    /// Warning finding.
    /// </summary>
    Warning,

    /// <summary>
    /// Error finding.
    /// </summary>
    Error,
}
