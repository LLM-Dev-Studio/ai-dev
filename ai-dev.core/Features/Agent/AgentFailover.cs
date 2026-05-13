namespace AiDev.Features.Agent;

/// <summary>
/// Records when an agent falls back to a different executor.
/// </summary>
/// <param name="Executor">The executor selected during failover.</param>
/// <param name="OccurredAt">The UTC timestamp when the failover occurred.</param>
public sealed record AgentFailover(AgentExecutorName Executor, DateTime OccurredAt);
