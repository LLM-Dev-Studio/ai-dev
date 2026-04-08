namespace AiDev.Models;

public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct = default);
}
