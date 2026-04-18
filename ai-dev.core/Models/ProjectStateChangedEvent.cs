namespace AiDev.Models;

public sealed record ProjectStateChangedEvent(
    ProjectSlug ProjectSlug,
    ProjectStateChangeKind Kind,
    DateTime OccurredAt);
