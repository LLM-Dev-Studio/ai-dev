using AiDev.Executors;

namespace AiDev.Services;

/// <summary>
/// Background service that polls every registered IAgentExecutor's CheckHealthAsync
/// and caches the results. Replaces the previous Ollama-specific OllamaHealthService.
///
/// OverwatchService injects this to decide whether a stalled agent's executor is
/// available before sending a nudge. The UI uses it to show executor health indicators.
/// </summary>
public class ExecutorHealthMonitor(
    IEnumerable<IAgentExecutor> executors,
    ILogger<ExecutorHealthMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(8);

    private readonly IReadOnlyList<IAgentExecutor> _executors = [.. executors];
    private readonly ConcurrentDictionary<string, ExecutorHealthResult> _cache = new(StringComparer.Ordinal);

    private event Action? Changed;
    private event Action<string, ExecutorHealthResult, ExecutorHealthResult>? Transitioned;

    public DateTimeOffset? LastChecked { get; private set; }

    /// <summary>Subscribes to poll-cycle updates and returns a disposable subscription.</summary>
    public IDisposable SubscribeChanged(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Changed += handler;
        return new DelegateSubscription(() => Changed -= handler);
    }

    /// <summary>Subscribes to health transition notifications and returns a disposable subscription.</summary>
    public IDisposable SubscribeTransitioned(Action<string, ExecutorHealthResult, ExecutorHealthResult> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Transitioned += handler;
        return new DelegateSubscription(() => Transitioned -= handler);
    }

    /// <summary>Returns the cached health result for the named executor, or an "unknown" result if not yet polled.</summary>
    public ExecutorHealthResult GetHealth(string executorName)
        => _cache.TryGetValue(executorName, out var result)
            ? result
            : new ExecutorHealthResult(false, "Not yet checked");

    /// <summary>Returns a snapshot of all cached health results keyed by executor name.</summary>
    public IReadOnlyDictionary<string, ExecutorHealthResult> GetAllHealth()
        => new Dictionary<string, ExecutorHealthResult>(_cache, StringComparer.Ordinal);

    /// <summary>Returns all registered executors with their current health, for UI rendering.</summary>
    public IReadOnlyList<(IAgentExecutor Executor, ExecutorHealthResult Health)> GetExecutorHealth()
        => _executors
            .Select(e => (e, GetHealth(e.Name)))
            .ToList();

    /// <summary>Triggers an on-demand health poll across all executors.</summary>
    public Task RefreshAsync(CancellationToken ct = default) => PollAllAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Check immediately on startup so the UI has data before the first 30-second tick.
        await PollAllAsync(ct).ConfigureAwait(false);

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await PollAllAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task PollAllAsync(CancellationToken ct)
    {
        var checks = _executors.Select(executor => CheckExecutorAsync(executor, ct));
        await Task.WhenAll(checks).ConfigureAwait(false);

        LastChecked = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    private async Task CheckExecutorAsync(IAgentExecutor executor, CancellationToken ct)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var checkedAt = DateTimeOffset.UtcNow;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(HealthCheckTimeout);

            var result = await executor.CheckHealthAsync(timeoutCts.Token).ConfigureAwait(false);
            var enriched = result with
            {
                CheckedAt = checkedAt,
                Duration = Stopwatch.GetElapsedTime(startedAt)
            };

            SetResult(executor.Name, enriched);
            logger.LogDebug("[executor-health] {Executor}: healthy={Healthy} — {Message}",
                executor.Name, enriched.IsHealthy, enriched.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            var result = new ExecutorHealthResult(
                false,
                $"Health check timed out after {HealthCheckTimeout.TotalSeconds:0} seconds",
                CheckedAt: checkedAt,
                Duration: Stopwatch.GetElapsedTime(startedAt));

            SetResult(executor.Name, result);
            logger.LogDebug("[executor-health] Check timed out for {Executor}", executor.Name);
        }
        catch (Exception ex)
        {
            var result = new ExecutorHealthResult(
                false,
                ex.Message,
                CheckedAt: checkedAt,
                Duration: Stopwatch.GetElapsedTime(startedAt));

            SetResult(executor.Name, result);
            logger.LogDebug(ex, "[executor-health] Check failed for {Executor}", executor.Name);
        }
    }

    private void SetResult(string executorName, ExecutorHealthResult current)
    {
        var hadPrevious = _cache.TryGetValue(executorName, out var previous);
        _cache[executorName] = current;

        if (hadPrevious && previous is not null && previous.IsHealthy != current.IsHealthy)
        {
            Transitioned?.Invoke(executorName, previous, current);
        }
    }

    private sealed class DelegateSubscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
