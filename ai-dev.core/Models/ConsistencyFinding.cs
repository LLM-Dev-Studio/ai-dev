namespace AiDev.Models;

/// <summary>
/// Represents a single consistency finding reported during validation.
/// </summary>
/// <param name="Code">The machine-readable finding code.</param>
/// <param name="Severity">The severity of the finding.</param>
/// <param name="Message">The human-readable finding message.</param>
/// <param name="FixType">The fix classification for the finding.</param>
/// <param name="ProjectSlug">The optional project slug associated with the finding.</param>
/// <param name="ResourceId">The optional resource identifier associated with the finding.</param>
public sealed record ConsistencyFinding(
    string Code,
    ConsistencySeverity Severity,
    string Message,
    ConsistencyFixType FixType = ConsistencyFixType.None,
    string? ProjectSlug = null,
    string? ResourceId = null);
