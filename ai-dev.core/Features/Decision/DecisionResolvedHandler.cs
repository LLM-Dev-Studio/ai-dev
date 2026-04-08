namespace AiDev.Features.Decision;

internal sealed class DecisionResolvedHandler(ILogger<DecisionResolvedHandler> logger) : IDomainEventHandler<DecisionResolved>
{
    public Task Handle(DecisionResolved domainEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation("[decisions] Dispatched DecisionResolved for {DecisionId} by {ResolvedBy}",
            domainEvent.DecisionId, domainEvent.ResolvedBy);
        return Task.CompletedTask;
    }
}
