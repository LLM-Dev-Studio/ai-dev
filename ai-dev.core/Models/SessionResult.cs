namespace AiDev.Models;

/// <summary>
/// Machine-readable artifact written by an agent to its outbox when it completes a task.
/// Written to: workspaces/{project}/agents/{slug}/outbox/result.json
/// Persisted alongside the session transcript as: {date}.result.json
/// </summary>
public record SessionResult(
    string? TaskId,
    [property: JsonPropertyName("status")] SessionStatus? SessionStatus,
    string? Summary,
    string? PullRequestUrl,
    IReadOnlyList<string> FilesChanged,
    TestOutcome? TestOutcome,
    DateTime? CompletedAt,
    IReadOnlyList<string>? Tags = null);

