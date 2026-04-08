using System.Reflection;

namespace AiDev.Services;

internal sealed class InProcessDomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<InProcessDomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly MethodInfo DispatchMethod = typeof(InProcessDomainEventDispatcher)
        .GetMethod(nameof(DispatchCore), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public async Task<Result<Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        var failures = new List<string>();

        foreach (var domainEvent in events)
        {
            ct.ThrowIfCancellationRequested();
            var method = DispatchMethod.MakeGenericMethod(domainEvent.GetType());
            var task = (Task<Result<Unit>>)method.Invoke(this, [domainEvent, ct])!;
            var result = await task.ConfigureAwait(false);
            if (result is Err<Unit> err)
                failures.Add(err.Error.Message);
        }

        return failures.Count == 0
            ? new Ok<Unit>(Unit.Value)
            : CountFailureResult(failures);
    }

    private async Task<Result<Unit>> DispatchCore<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : DomainEvent
    {
        var failures = new List<string>();

        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
        {
            try
            {
                await handler.Handle(domainEvent, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[dispatcher] Handler {Handler} failed for {Event}",
                    handler.GetType().Name, typeof(TEvent).Name);
                failures.Add($"{handler.GetType().Name}: {ex.Message}");
            }
        }

        return failures.Count == 0
            ? new Ok<Unit>(Unit.Value)
            : CountFailureResult(failures);
    }

    private static Err<Unit> CountFailureResult(List<string> failures)
    {
        AiDevTelemetry.DomainDispatchFailures.Add(failures.Count);
        return new Err<Unit>(new DomainError("DOMAIN_EVENT_HANDLER_FAILED", string.Join(Environment.NewLine, failures)));
    }
}
