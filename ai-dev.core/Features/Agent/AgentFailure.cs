namespace AiDev.Features.Agent;

/// <summary>
/// Represents details about a failed agent run.
/// </summary>
/// <param name="Error">The failure message.</param>
/// <param name="OccurredAt">The UTC timestamp when the failure occurred.</param>
public sealed record AgentFailure(string Error, DateTime OccurredAt);
