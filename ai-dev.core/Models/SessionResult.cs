namespace AiDev.Models;

/// <summary>
/// Machine-readable artifact written by an agent to its outbox when it completes a task.
/// Written to: workspaces/{project}/agents/{slug}/outbox/result.json
/// Persisted alongside the session transcript as: {date}.result.json
/// </summary>
public record SessionResult(
    string? TaskId,
    string? Status,           // completed | failed | partial
    string? Summary,
    string? PullRequestUrl,
    IReadOnlyList<string> FilesChanged,
    string? TestOutcome,      // passed | failed | skipped | null
    DateTime? CompletedAt,
    IReadOnlyList<string>? Tags = null);
