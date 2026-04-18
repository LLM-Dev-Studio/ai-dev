using AiDev;
using AiDev.Features.Board;
using AiDev.Features.Workspace;
using AiDev.Models;
using AiDev.Models.Types;
using AiDev.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Integration;

public class ConsistencyUiAndDetectionIntegrationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly WorkspacePaths _paths;
    private readonly AtomicFileWriter _fileWriter = new();
    private readonly ProjectMutationCoordinator _coordinator = new();

    public ConsistencyUiAndDetectionIntegrationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"consistency-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _paths = new WorkspacePaths(new RootDir(_rootPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public async Task CheckProjectAsync_WhenRawBoardHasDuplicateTaskReference_ReportsRawFinding()
    {
        var workspaceService = new WorkspaceService(_paths, _fileWriter);
        workspaceService.CreateProject("demo-project", "Demo Project", null).ShouldBeOfType<Ok<Unit>>();
        var projectSlug = new ProjectSlug("demo-project");
        var boardPath = _paths.BoardPath(projectSlug);
        _fileWriter.WriteAllText(boardPath, "{\"columns\":[{\"id\":\"backlog\",\"title\":\"Backlog\",\"taskIds\":[\"task-1\"]},{\"id\":\"review\",\"title\":\"Review\",\"taskIds\":[\"task-1\"]}],\"tasks\":{\"task-1\":{\"id\":\"task-1\",\"title\":\"Investigate failure\",\"priority\":\"normal\"}}}");

        var service = new ConsistencyCheckService(
            _paths,
            workspaceService,
            new BoardService(_paths, new PassingDispatcher(), _fileWriter, _coordinator, NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier()),
            NullLogger<ConsistencyCheckService>.Instance);

        var report = await service.CheckProjectAsync(projectSlug, TestContext.Current.CancellationToken);

        report.Findings.ShouldContain(f => f.Code == "BOARD_TASK_DUPLICATE_REFERENCE_RAW");
    }

    private sealed class PassingDispatcher : IDomainEventDispatcher
    {
        public Task<Result<Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
            => Task.FromResult<Result<Unit>>(new Ok<Unit>(Unit.Value));
    }
}
