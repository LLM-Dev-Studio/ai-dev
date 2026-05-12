namespace AiDev.Models;

/// <summary>
/// Represents a project state change notification.
/// </summary>
/// <param name="ProjectSlug">The project whose state changed.</param>
/// <param name="Kind">The kind of state that changed.</param>
/// <param name="OccurredAt">The UTC timestamp when the change occurred.</param>
public sealed record ProjectStateChangedEvent(
    ProjectSlug ProjectSlug,
    ProjectStateChangeKind Kind,
    DateTime OccurredAt);
