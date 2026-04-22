using AiDev.Core.Local.Contracts;
using System.Text.Json;

namespace AiDev.Core.Local.Implementation;

internal sealed class FileSystemRuntimeMemoryStore : IRuntimeMemoryStore
{
    private readonly string _memoryDir;

    public FileSystemRuntimeMemoryStore(WorkspacePaths paths)
        : this(Path.Combine(paths.Root.Value, ".ai-dev", "memory"))
    {
    }

    // Internal constructor for testing with an explicit directory.
    internal FileSystemRuntimeMemoryStore(string memoryDir)
    {
        _memoryDir = memoryDir;
    }

    public async Task<Result<Unit>> SaveSnapshotAsync(
        Guid objectiveId,
        CompactionSnapshot snapshot,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(_memoryDir);
        var path = SnapshotPath(objectiveId);
        var json = JsonSerializer.Serialize(snapshot, JsonDefaults.Write);
        await File.WriteAllTextAsync(path, json, ct);
        return new Ok<Unit>(default);
    }

    public async Task<Result<IReadOnlyList<RuntimeFact>>> LoadFactsAsync(
        Guid objectiveId,
        CancellationToken ct = default)
    {
        var path = SnapshotPath(objectiveId);
        if (!File.Exists(path))
            return new Ok<IReadOnlyList<RuntimeFact>>([]);

        var json = await File.ReadAllTextAsync(path, ct);
        var snapshot = JsonSerializer.Deserialize<CompactionSnapshot>(json, JsonDefaults.Read);
        IReadOnlyList<RuntimeFact> facts = snapshot?.Facts ?? [];
        return new Ok<IReadOnlyList<RuntimeFact>>(facts);
    }

    private string SnapshotPath(Guid objectiveId) =>
        Path.Combine(_memoryDir, $"{objectiveId:N}.json");
}
