namespace AiDev.Models;

/// <summary>
/// Base type for domain events raised by aggregates and services.
/// </summary>
/// <param name="OccurredAt">The UTC timestamp when the event occurred.</param>
public abstract record DomainEvent(DateTime OccurredAt);
