using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation;

internal sealed class InMemoryRuntimeMemoryStore : IRuntimeMemoryStore
{
    private readonly ConcurrentDictionary<Guid, CompactionSnapshot> _store = new();

    public Task<Result<Unit>> SaveSnapshotAsync(Guid objectiveId, CompactionSnapshot snapshot, CancellationToken ct = default)
    {
        _store[objectiveId] = snapshot;
        return Task.FromResult<Result<Unit>>(new Ok<Unit>(default));
    }

    public Task<Result<IReadOnlyList<RuntimeFact>>> LoadFactsAsync(Guid objectiveId, CancellationToken ct = default)
    {
        IReadOnlyList<RuntimeFact> facts = _store.TryGetValue(objectiveId, out var snapshot)
            ? snapshot.Facts
            : [];
        return Task.FromResult<Result<IReadOnlyList<RuntimeFact>>>(new Ok<IReadOnlyList<RuntimeFact>>(facts));
    }
}
