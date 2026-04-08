namespace AiDev.Services;

/// <summary>
/// Coordinates project-scoped asynchronous mutations so file-backed writes do not race within the process.
/// </summary>
public class ProjectMutationCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public T Execute<T>(ProjectSlug projectSlug, Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(projectSlug);
        ArgumentNullException.ThrowIfNull(operation);

        var gate = _locks.GetOrAdd(projectSlug.Value, _ => new SemaphoreSlim(1, 1));
        var startedAt = Stopwatch.GetTimestamp();
        gate.Wait();
        AiDevTelemetry.ProjectLockWaitMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("project", projectSlug.Value));
        try
        {
            return operation();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> ExecuteAsync<T>(ProjectSlug projectSlug, Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectSlug);
        ArgumentNullException.ThrowIfNull(operation);

        var gate = _locks.GetOrAdd(projectSlug.Value, _ => new SemaphoreSlim(1, 1));
        var startedAt = Stopwatch.GetTimestamp();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        AiDevTelemetry.ProjectLockWaitMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("project", projectSlug.Value));
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<T> ExecuteAsync<T>(ProjectSlug projectSlug, Func<T> operation, CancellationToken cancellationToken = default)
        => ExecuteAsync(projectSlug, () => Task.FromResult(operation()), cancellationToken);
}
