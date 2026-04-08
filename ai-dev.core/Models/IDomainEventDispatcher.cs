namespace AiDev.Models;

public interface IDomainEventDispatcher
{
    Task<Result<Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default);
}
