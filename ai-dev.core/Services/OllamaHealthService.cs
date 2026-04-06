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
    private readonly IReadOnlyList<IAgentExecutor> _executors = [.. executors];
    private readonly ConcurrentDictionary<string, ExecutorHealthResult> _cache = new(StringComparer.Ordinal);

    /// <summary>Fired after every poll cycle (not just on transitions) so the UI stays current.</summary>
    public event Action? Changed;

    public DateTimeOffset? LastChecked { get; private set; }

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

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Check immediately on startup so the UI has data before the first 30-second tick.
        while (!ct.IsCancellationRequested)
        {
            await PollAllAsync(ct).ConfigureAwait(false);
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollAllAsync(CancellationToken ct)
    {
        foreach (var executor in _executors)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await executor.CheckHealthAsync(ct).ConfigureAwait(false);
                _cache[executor.Name] = result;
                logger.LogDebug("[executor-health] {Executor}: healthy={Healthy} — {Message}",
                    executor.Name, result.IsHealthy, result.Message);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[executor-health] Check failed for {Executor}", executor.Name);
                _cache[executor.Name] = new ExecutorHealthResult(false, ex.Message);
            }
        }

        LastChecked = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }
}
