namespace AiDev.Models;

/// <summary>
/// Raised when a decision is resolved.
/// </summary>
/// <param name="DecisionId">The resolved decision identifier.</param>
/// <param name="ResolvedBy">The actor that resolved the decision.</param>
/// <param name="OccurredAt">The UTC timestamp when the decision was resolved.</param>
public sealed record DecisionResolved(
    DecisionId DecisionId,
    string ResolvedBy,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
