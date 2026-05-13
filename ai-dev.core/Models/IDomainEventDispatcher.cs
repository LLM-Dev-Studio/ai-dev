namespace AiDev.Models;

/// <summary>
/// Dispatches domain events to registered handlers.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches the provided domain events.
    /// </summary>
    /// <param name="events">The events to dispatch.</param>
    /// <param name="ct">The cancellation token for the dispatch operation.</param>
    /// <returns>The result of the dispatch operation.</returns>
    Task<Result<Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default);
}
