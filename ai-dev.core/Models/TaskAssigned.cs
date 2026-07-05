namespace AiDev.Models;

/// <summary>
/// Raised when a task is assigned to an agent.
/// </summary>
/// <param name="ProjectSlug">The project that owns the task.</param>
/// <param name="TaskId">The assigned task identifier.</param>
/// <param name="Assignee">The agent assigned to the task.</param>
/// <param name="Title">The task title.</param>
/// <param name="Description">The optional task description.</param>
/// <param name="Priority">The task priority.</param>
/// <param name="OccurredAt">The UTC timestamp when the assignment occurred.</param>
public sealed record TaskAssigned(
    ProjectSlug ProjectSlug,
    TaskId TaskId,
    AgentSlug Assignee,
    string Title,
    string? Description,
    Priority Priority,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
