namespace AiDev.Models;

/// <summary>
/// Handles a specific type of domain event.
/// </summary>
/// <typeparam name="TEvent">The domain event type handled by this handler.</typeparam>
public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    /// <summary>
    /// Handles the provided domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event to handle.</param>
    /// <param name="ct">The cancellation token for the handler operation.</param>
    Task Handle(TEvent domainEvent, CancellationToken ct = default);
}
