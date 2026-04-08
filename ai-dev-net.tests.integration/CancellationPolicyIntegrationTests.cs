using AiDev;
using AiDev.Features.Board;
using AiDev.Models;
using AiDev.Models.Types;
using AiDev.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Integration;

public class CancellationPolicyIntegrationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly WorkspacePaths _paths;
    private readonly AtomicFileWriter _fileWriter = new();
    private readonly ProjectMutationCoordinator _coordinator = new();

    public CancellationPolicyIntegrationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _paths = new WorkspacePaths(new RootDir(_rootPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var service = new BoardService(_paths, new PassingDispatcher(), _fileWriter, _coordinator, NullLogger<BoardService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");
        service.SaveBoard(projectSlug, new Board(projectSlug));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.CreateTaskAsync(projectSlug, ColumnId.Backlog.Value, "Investigate failure", null, Priority.Normal.Value, null, cts.Token));
    }

    private sealed class PassingDispatcher : IDomainEventDispatcher
    {
        public Task<Result<Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
            => Task.FromResult<Result<Unit>>(new Ok<Unit>(Unit.Value));
    }
}
