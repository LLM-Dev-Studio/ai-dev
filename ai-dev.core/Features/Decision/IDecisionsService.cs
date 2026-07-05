namespace AiDev.Features.Decision;

/// <summary>
/// Provides creation, lookup, and resolution operations for project decisions.
/// </summary>
public interface IDecisionsService
{
    /// <summary>
    /// Creates a new decision request.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="from">The decision source.</param>
    /// <param name="subject">The decision subject.</param>
    /// <param name="priority">The decision priority.</param>
    /// <param name="blocks">The optional blocker reference.</param>
    /// <param name="body">The decision body content.</param>
    /// <returns>The result of the create operation.</returns>
    Result<Unit> CreateDecision(ProjectSlug projectSlug, string from, string subject,
        Priority priority, string? blocks, string body);

    /// <summary>
    /// Lists decisions for a project filtered by status.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decisions.</param>
    /// <param name="status">The optional status filter.</param>
    /// <returns>The matching decisions.</returns>
    List<DecisionItem> ListDecisions(ProjectSlug projectSlug, DecisionStatus? status = null);

    /// <summary>
    /// Gets a decision by identifier.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="id">The decision identifier.</param>
    /// <returns>The decision item, or <see langword="null"/> when not found.</returns>
    DecisionItem? GetDecision(ProjectSlug projectSlug, DecisionId id);

    /// <summary>
    /// Resolves a decision with a human response.
    /// </summary>
    /// <param name="projectSlug">The project that owns the decision.</param>
    /// <param name="id">The decision identifier.</param>
    /// <param name="response">The response used to resolve the decision.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The result of the resolve operation.</returns>
    Task<Result<Unit>> ResolveDecisionAsync(ProjectSlug projectSlug, DecisionId id, string response, CancellationToken cancellationToken);
}
