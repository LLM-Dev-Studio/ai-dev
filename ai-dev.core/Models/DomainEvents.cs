namespace AiDev.Models;

public abstract record DomainEvent(DateTime OccurredAt);

public sealed record TaskAssigned(
    TaskId TaskId,
    AgentSlug Assignee,
    string Title,
    string? Description,
    Priority Priority,
    DateTime OccurredAt) : DomainEvent(OccurredAt);

public sealed record DecisionResolved(
    string DecisionId,
    string ResolvedBy,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
