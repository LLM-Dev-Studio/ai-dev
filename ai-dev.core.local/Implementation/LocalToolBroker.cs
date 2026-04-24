using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Orchestration;

namespace AiDev.Core.Local.Implementation;

internal sealed class LocalToolBroker : ILocalToolBroker
{
    private readonly string _root;
    private readonly SemaphoreSlim _semaphore;

    public LocalToolBroker(WorkspacePaths paths, LocalOrchestratorOptions options)
        : this(paths.Root.Value, options.MaxParallelTools) { }

    internal LocalToolBroker(string root, int maxParallelTools = 1)
    {
        _root = root;
        _semaphore = new SemaphoreSlim(Math.Max(1, maxParallelTools));
    }

    public async Task<Result<IReadOnlyList<ToolOutcome>>> ExecuteAsync(
        IReadOnlyList<ToolRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
            return new Ok<IReadOnlyList<ToolOutcome>>([]);

        var tasks = requests.Select(r => ExecuteWithSemaphoreAsync(r, ct));
        var outcomes = await Task.WhenAll(tasks);
        return new Ok<IReadOnlyList<ToolOutcome>>(outcomes);
    }

    private async Task<ToolOutcome> ExecuteWithSemaphoreAsync(ToolRequest request, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try { return await ExecuteOneAsync(request, ct); }
        finally { _semaphore.Release(); }
    }

    private async Task<ToolOutcome> ExecuteOneAsync(ToolRequest request, CancellationToken ct)
        => request.ToolName switch
        {
            "read_file" => await ReadFileAsync(request, ct),
            "list_dir"  => ListDir(request),
            "grep"      => await GrepAsync(request, ct),
            "glob"      => Glob(request),
            _ => new ToolOutcome(
                request.ToolName,
                Succeeded: false,
                Summary: $"Unknown tool '{request.ToolName}'.",
                Evidence: [],
                Error: new DomainError("ToolBroker.UnknownTool", $"No handler registered for tool '{request.ToolName}'."))
        };

    private async Task<ToolOutcome> ReadFileAsync(ToolRequest request, CancellationToken ct)
    {
        if (!request.Arguments.TryGetValue("path", out var relativePath))
            return Fail(request.ToolName, "read_file requires 'path' argument.");

        var fullPath = Resolve(relativePath);
        if (!File.Exists(fullPath))
            return Fail(request.ToolName, $"File not found: {relativePath}");

        var content = await File.ReadAllTextAsync(fullPath, ct);
        var lines = content.Split('\n').Length;
        return new ToolOutcome(
            request.ToolName,
            Succeeded: true,
            Summary: $"Read {lines} lines from {relativePath}",
            Evidence: [$"{fullPath}:1"]);
    }

    private ToolOutcome ListDir(ToolRequest request)
    {
        var dir = request.Arguments.TryGetValue("path", out var p)
            ? Resolve(p)
            : _root;

        if (!Directory.Exists(dir))
            return Fail(request.ToolName, $"Directory not found: {dir}");

        var entries = Directory
            .EnumerateFileSystemEntries(dir)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n)
            .ToList();

        return new ToolOutcome(
            request.ToolName,
            Succeeded: true,
            Summary: $"{entries.Count} item(s) in {Path.GetRelativePath(_root, dir)}",
            Evidence: entries.Select(e => Path.Combine(dir, e)).Take(20).ToList());
    }

    private async Task<ToolOutcome> GrepAsync(ToolRequest request, CancellationToken ct)
    {
        if (!request.Arguments.TryGetValue("pattern", out var pattern))
            return Fail(request.ToolName, "grep requires 'pattern' argument.");

        var searchDir = request.Arguments.TryGetValue("dir", out var d) ? Resolve(d) : _root;
        var extension = request.Arguments.TryGetValue("extension", out var ext) ? ext : "*";

        if (!Directory.Exists(searchDir))
            return Fail(request.ToolName, $"Directory not found: {searchDir}");

        var matches = new List<string>();
        foreach (var file in Directory.EnumerateFiles(searchDir, $"*.{extension.TrimStart('*', '.')}", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var lineNum = 0;
            foreach (var line in await File.ReadAllLinesAsync(file, ct))
            {
                lineNum++;
                if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    matches.Add($"{file}:{lineNum}");
            }
            if (matches.Count >= 50) break;
        }

        return new ToolOutcome(
            request.ToolName,
            Succeeded: true,
            Summary: $"grep '{pattern}': {matches.Count} match(es)",
            Evidence: matches);
    }

    private ToolOutcome Glob(ToolRequest request)
    {
        if (!request.Arguments.TryGetValue("pattern", out var globPattern))
            return Fail(request.ToolName, "glob requires 'pattern' argument.");

        var searchDir = request.Arguments.TryGetValue("dir", out var d) ? Resolve(d) : _root;
        if (!Directory.Exists(searchDir))
            return Fail(request.ToolName, $"Directory not found: {searchDir}");

        var files = Directory
            .EnumerateFiles(searchDir, globPattern, SearchOption.AllDirectories)
            .Take(100)
            .ToList();

        return new ToolOutcome(
            request.ToolName,
            Succeeded: true,
            Summary: $"glob '{globPattern}': {files.Count} file(s)",
            Evidence: files);
    }

    private string Resolve(string relativePath)
        => Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(_root, relativePath);

    private static ToolOutcome Fail(string toolName, string message)
        => new(toolName, Succeeded: false, Summary: message, Evidence: [],
            Error: new DomainError("ToolBroker.ExecutionFailed", message));
}
