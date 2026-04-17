using AiDev.Executors;

namespace AiDev.Services;

/// <summary>
/// Aggregates models from all registered executors.
/// For each executor the effective model list is:
///   KnownModels (static) + Models from the most-recent health-check (dynamic),
///   with dynamic entries winning on Id collision.
///
/// Registered as a singleton and refreshed whenever ExecutorHealthMonitor fires Changed.
/// </summary>
public sealed class ModelRegistry : IModelRegistry, IDisposable
{
    private readonly IReadOnlyList<IAgentExecutor> _executors;
    private readonly ExecutorHealthMonitor _healthMonitor;
    private readonly IDisposable _healthChangedSubscription;

    // Snapshot rebuilt on every Changed event.
    private volatile IReadOnlyDictionary<string, IReadOnlyList<ModelDescriptor>> _byExecutor;

    public ModelRegistry(IEnumerable<IAgentExecutor> executors, ExecutorHealthMonitor healthMonitor)
    {
        _executors = [.. executors];
        _healthMonitor = healthMonitor;
        _byExecutor = Build();

        _healthChangedSubscription = _healthMonitor.SubscribeChanged(Refresh);
    }

    public IReadOnlyList<ModelDescriptor> GetModelsForExecutor(string executorName)
        => _byExecutor.TryGetValue(executorName, out var list) ? list : [];

    public ModelDescriptor? Find(string executorName, string modelId)
        => GetModelsForExecutor(executorName)
            .FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ModelDescriptor> GetAll()
        => [.. _byExecutor.Values.SelectMany(v => v)];

    public void Dispose() => _healthChangedSubscription.Dispose();

    // -------------------------------------------------------------------------

    private void Refresh() => _byExecutor = Build();

    private IReadOnlyDictionary<string, IReadOnlyList<ModelDescriptor>> Build()
    {
        var dict = new Dictionary<string, IReadOnlyList<ModelDescriptor>>(StringComparer.OrdinalIgnoreCase);

        foreach (var executor in _executors)
        {
            // Start with static known models (keyed by Id for merging).
            var merged = executor.KnownModels.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

            // Dynamic models from the most-recent health check add NEW models only.
            // Static entries win on Id collision — they carry richer capability metadata.
            var health = _healthMonitor.GetHealth(executor.Name);
            if (health.Models is { Count: > 0 } discovered)
            {
                foreach (var m in discovered)
                {
                    if (!merged.ContainsKey(m.Id))
                        merged[m.Id] = m;
                }
            }

            dict[executor.Name] = [.. merged.Values.OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)];
        }

        return dict;
    }
}
