using AiDev;
using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;
using AiDev.Features.KnowledgeBase;
using AiDev.Features.Playbook;
using AiDev.Features.Workspace;
using AiDev.Models;
using AiDev.Models.Types;
using AiDev.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Integration;

public class ProductionReadinessIntegrationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly WorkspacePaths _paths;
    private readonly AtomicFileWriter _fileWriter = new();
    private readonly ProjectMutationCoordinator _coordinator = new();

    public ProductionReadinessIntegrationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"prod-ready-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _paths = new WorkspacePaths(new RootDir(_rootPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public async Task BoardService_CreateTaskAsync_WhenHandlerFails_ReturnsTypedError()
    {
        var dispatcher = new StubDispatcher(new Err<Unit>(new DomainError("DOMAIN_EVENT_HANDLER_FAILED", "task handler failed")));
        var service = new BoardService(_paths, dispatcher, _fileWriter, _coordinator, NullLogger<BoardService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");

        service.SaveBoard(projectSlug, new Board(projectSlug));

        var result = await service.CreateTaskAsync(projectSlug, ColumnId.Backlog.Value, "Investigate failure", null, Priority.Normal.Value, "backend-dev");

        result.ShouldBeOfType<Err<BoardTask>>();
        ((Err<BoardTask>)result).Error.Code.ShouldBe("DOMAIN_EVENT_HANDLER_FAILED");
    }

    [Fact]
    public void BoardService_SaveAndLoadBoard_RoundTripsExistingBoardState()
    {
        var service = new BoardService(_paths, new StubDispatcher(new Ok<Unit>(Unit.Value)), _fileWriter, _coordinator, NullLogger<BoardService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");
        var taskId = TaskId.New();
        var board = new Board(
            projectSlug,
            [
                new BoardColumn(ColumnId.Backlog, "Backlog", [taskId]),
                new BoardColumn(ColumnId.InProgress, "In Progress"),
                new BoardColumn(ColumnId.Review, "Review"),
                new BoardColumn(ColumnId.Done, "Done")
            ],
            new Dictionary<TaskId, BoardTask>
            {
                [taskId] = new BoardTask(taskId, "Investigate failure", description: "Check persistence")
            });

        service.SaveBoard(projectSlug, board);
        File.ReadAllText(_paths.BoardPath(projectSlug)).ShouldContain(taskId.Value);
        var loaded = service.LoadBoard(projectSlug);

        loaded.Tasks.Count.ShouldBe(1);
        loaded.Tasks.ShouldContainKey(taskId);
        loaded.Tasks[taskId].Title.ShouldBe("Investigate failure");
        loaded.Tasks[taskId].Description.ShouldBe("Check persistence");
        loaded.Columns.First(c => c.Id == ColumnId.Backlog).TaskIds.ShouldContain(taskId);
        loaded.Columns.Select(c => c.Id).ShouldBe([ColumnId.Backlog, ColumnId.InProgress, ColumnId.Review, ColumnId.Done]);
    }

    [Fact]
    public void BoardService_LoadBoard_WhenBoardJsonIsValid_DoesNotCreateCorruptBackup()
    {
        var service = new BoardService(_paths, new StubDispatcher(new Ok<Unit>(Unit.Value)), _fileWriter, _coordinator, NullLogger<BoardService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");
        var boardPath = _paths.BoardPath(projectSlug);
        Directory.CreateDirectory(Path.GetDirectoryName(boardPath)!);
        File.WriteAllText(boardPath, """
            {
              "columns": [
                { "id": "backlog", "title": "Backlog", "taskIds": [] },
                { "id": "in-progress", "title": "In Progress", "taskIds": [] },
                { "id": "review", "title": "Review", "taskIds": [] },
                { "id": "done", "title": "Done", "taskIds": [] }
              ],
              "tasks": {}
            }
            """);

        var loaded = service.LoadBoard(projectSlug);

        loaded.Columns.Count.ShouldBe(4);
        Directory.GetFiles(Path.GetDirectoryName(boardPath)!, "board.json.corrupt.*").ShouldBeEmpty();
    }

    [Fact]
    public async Task DecisionsService_ResolveDecisionAsync_WhenHandlerFails_ReturnsTypedError()
    {
        var dispatcher = new StubDispatcher(new Err<Unit>(new DomainError("DOMAIN_EVENT_HANDLER_FAILED", "decision handler failed")));
        var service = new DecisionsService(_paths, dispatcher, _fileWriter, _coordinator, NullLogger<DecisionsService>.Instance);
        var projectSlug = new ProjectSlug("demo-project");

        var createResult = service.CreateDecision(projectSlug, "overwatch", "Need input", Priority.Normal.Value, null, "Please decide");
        createResult.ShouldBeOfType<Ok<Unit>>();

        var id = Path.GetFileNameWithoutExtension(Directory.GetFiles(_paths.DecisionsPendingDir(projectSlug), "*.md").Single())!;
        var result = await service.ResolveDecisionAsync(projectSlug, id, "Proceed", TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Err<Unit>>();
        ((Err<Unit>)result).Error.Code.ShouldBe("DOMAIN_EVENT_HANDLER_FAILED");
    }

    [Fact]
    public void AtomicFileWriter_WhenOverwritingFile_ReplacesContent()
    {
        var path = Path.Combine(_rootPath, "data", "state.json");
        _fileWriter.WriteAllText(path, "one");
        _fileWriter.WriteAllText(path, "two");

        File.ReadAllText(path).ShouldBe("two");
    }

    [Fact]
    public void WorkspaceService_CreateProject_UsesAtomicWritesForArtifacts()
    {
        var service = new WorkspaceService(_paths, _fileWriter);

        var result = service.CreateProject("demo-project", "Demo Project", "Main app");

        result.ShouldBeOfType<Ok<Unit>>();
        File.Exists(_paths.ProjectJsonPath(new ProjectSlug("demo-project"))).ShouldBeTrue();
        File.Exists(_paths.BoardPath(new ProjectSlug("demo-project"))).ShouldBeTrue();
        File.Exists(_paths.RegistryPath).ShouldBeTrue();
    }

    [Fact]
    public void AgentService_CreateAgent_WritesFilesAtomically()
    {
        var service = new AgentService(
            _paths,
            new StudioSettingsService(_paths),
            new AgentTemplatesService(_paths),
            _fileWriter,
            _coordinator);
        var projectSlug = new ProjectSlug("demo-project");
        var workspaceService = new WorkspaceService(_paths, _fileWriter);
        workspaceService.CreateProject(projectSlug.Value, "Demo Project", null).ShouldBeOfType<Ok<Unit>>();

        var templateJsonPath = _paths.SafeTemplatePath("generic-standard", ".json")!;
        var templateMdPath = _paths.SafeTemplatePath("generic-standard", ".md")!;
        Directory.CreateDirectory(Path.GetDirectoryName(templateJsonPath.Value)!);
        File.WriteAllText(templateJsonPath.Value, "{\"slug\":\"generic-standard\",\"name\":\"Generic\",\"role\":\"Implement features\",\"model\":\"sonnet\",\"description\":\"Generalist\",\"content\":\"\"}");
        File.WriteAllText(templateMdPath.Value, "# Generic\n\nYou build features.");

        var result = service.CreateAgent(projectSlug, "backend-dev", "Backend Dev", "generic-standard");

        result.ShouldBeOfType<Ok<Unit>>();
        File.Exists(_paths.AgentJsonPath(projectSlug, new AgentSlug("backend-dev"))).ShouldBeTrue();
        File.Exists(_paths.AgentClaudeMdPath(projectSlug, new AgentSlug("backend-dev"))).ShouldBeTrue();
    }

    [Fact]
    public void KbAndPlaybookServices_SaveAtomically()
    {
        var projectSlug = new ProjectSlug("demo-project");
        var workspaceService = new WorkspaceService(_paths, _fileWriter);
        workspaceService.CreateProject(projectSlug.Value, "Demo Project", null).ShouldBeOfType<Ok<Unit>>();

        var kbService = new KbService(_paths, _fileWriter, _coordinator);
        var playbookService = new PlaybookService(_paths, _fileWriter, _coordinator);

        kbService.Save(projectSlug, "deployment", "# Deployment\n\nSteps").ShouldBeOfType<Ok<Unit>>();
        playbookService.Save(projectSlug, "deploy-check", "# Deploy Check\n\nSteps").ShouldBeOfType<Ok<Unit>>();

        File.Exists(Path.Combine(_paths.KbDir(projectSlug), "deployment.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_paths.PlaybooksDir(projectSlug), "deploy-check.md")).ShouldBeTrue();
    }

    private sealed class StubDispatcher(Result<Unit> result) : IDomainEventDispatcher
    {
        public Task<Result<Unit>> Dispatch(IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
            => Task.FromResult(result);
    }
}
