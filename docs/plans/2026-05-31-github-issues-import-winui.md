# GitHub Issues Import for WinUI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a one-way GitHub Issues import flow in the WinUI Board page that creates local ADS board tasks from open GitHub issues.

**Architecture:** The domain owns imported-task metadata and idempotent board writes. A core GitHub import service fetches open issues through a small GitHub client, maps them into board import candidates, and delegates persistence to `IBoardService`. WinUI provides the Board-page entry point, import dialog, preview selection, and result display.

**Tech Stack:** .NET 10, WinUI 3, CommunityToolkit.Mvvm, xUnit 3, Shouldly, `IHttpClientFactory`, GitHub REST API.

---

## Product Spec

### Source Issue

GitHub issue `LLM-Dev-Studio/ai-dev#25`, “Import Issues From Github”.

### User Story

As an ADS WinUI user with existing GitHub issues, I want to import selected open GitHub issues into my local ADS project board so agents can work from ADS without manually copying issue text.

### Decisions From Design Session

- Import direction is one-way: GitHub issues become local ADS board tasks.
- WinUI is the first client; no API route is added in v1.
- Import logic lives in shared .NET core services, not in WinUI view code.
- GitHub authentication uses the existing `StudioSettings.GitHubToken`.
- GitHub reads only open issues.
- GitHub writes are out of scope.
- GitHub Project v2 columns are out of scope.
- GitHub issue comments are out of scope.
- Optional exact GitHub label filtering is supported, but labels are not imported as ADS tags.
- Manual repository input accepts only `owner/repo`.
- The dialog auto-detects `owner/repo` from the active project codebase git remote, preferring `upstream` over `origin`, and allows manual override.
- Preview fetches up to 500 open issues, paged at 100 per request.
- Preview shows already-imported issues disabled and unchecked.
- Preview checks all not-yet-imported issues by default.
- Import re-checks dedupe at execution time.
- Existing imported issues are skipped, not updated.
- Deleting an imported ADS task removes the dedupe record, so the GitHub issue can be imported again later.
- Imported tasks are unassigned.
- Imported tasks have priority `normal`.
- Imported task descriptions preserve the full GitHub issue body and include the GitHub issue URL. Empty-body issues get a description containing only the URL line.
- GitHub labels are visible in preview only.
- Source metadata is visible in the task UI but not editable.
- Missing, invalid, unauthorized, and rate-limited GitHub token states are recoverable dialog errors.

### Acceptance Criteria

1. Board page has an `Import from GitHub` button next to `Refresh`.
2. Import dialog opens with detected `owner/repo` when the active codebase has a GitHub remote.
3. User can manually edit `owner/repo`.
4. User can choose any current ADS board column as the target, defaulting to `Backlog`.
5. User can enter an optional exact GitHub label filter.
6. Preview fetches only open GitHub issues and filters out pull requests.
7. Preview shows issue number, title, labels, created date, and already-imported status.
8. Already-imported issues are disabled and unchecked.
9. Import creates local board tasks for selected importable issues.
10. Re-import skips existing imported issues based on external source metadata.
11. Imported tasks contain no GitHub labels as ADS tags.
12. Imported tasks include full issue body and the source URL in description.
13. Imported tasks store source metadata: provider `github`, id `owner/repo#number`, and issue URL.
14. Task edit dialog shows source metadata read-only for imported tasks.
15. Clean import closes the dialog and refreshes the board.
16. Mixed import keeps the dialog open, refreshes the board if anything imported, and shows per-item results.
17. `dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj` passes.
18. `dotnet build ai-dev-net.slnx` passes.

### Out Of Scope

- GitHub OAuth or device-code authentication.
- Writing labels, comments, or project state back to GitHub.
- Importing GitHub issue comments.
- Importing closed issues.
- Mapping GitHub users to ADS agents.
- Importing GitHub labels into ADS task tags.
- API endpoints for import.
- VS Code extension changes.

---

## File Structure

### Domain And Board Persistence

- Modify `ai-dev.core/ai-dev.core.csproj`
  - Add `Microsoft.Extensions.Http` so core services can use `IHttpClientFactory`.
- Create `ai-dev.core/Features/Board/ExternalTaskSource.cs`
  - Generic value object for external task provenance.
- Create `ai-dev.core/Features/Board/BoardTaskImportCandidate.cs`
  - Input record for bulk board imports.
- Create `ai-dev.core/Features/Board/BoardTaskImportResult.cs`
  - Summary and per-item status for bulk imports.
- Modify `ai-dev.core/Features/Board/BoardTask.cs`
  - Add nullable read-only `ExternalSource`.
- Modify `ai-dev.core/Features/Board/BoardService.cs`
  - Persist external source metadata.
  - Add idempotent bulk import method.
- Modify `ai-dev.core/Features/Board/IBoardService.cs`
  - Add `ImportTasksAsync`.

### GitHub Import Core

- Create `ai-dev.core/Features/GitHub/GitHubRepository.cs`
  - Validates and parses `owner/repo`.
- Create `ai-dev.core/Features/GitHub/GitHubIssueSummary.cs`
  - Core issue DTO used by import services.
- Create `ai-dev.core/Features/GitHub/IGitHubIssuesClient.cs`
  - Read-only GitHub issue client contract.
- Create `ai-dev.core/Features/GitHub/GitHubIssuesClient.cs`
  - REST client with pagination, token handling, label filter, and error mapping.
- Create `ai-dev.core/Features/GitHub/GitHubIssueImportModels.cs`
  - Preview and import request/result records.
- Create `ai-dev.core/Features/GitHub/GitHubIssueImportService.cs`
  - Preview, dedupe marking, mapping, and board import orchestration.
- Create `ai-dev.core/Features/GitHub/GitHubRepositoryDetector.cs`
  - Reads `.git/config`, prefers `upstream`, then `origin`.
- Modify `ai-dev.core/Extensions/CoreServiceExtensions.cs`
  - Register GitHub client/import/detector services.
- Modify `ai-dev.core/Services/StudioSettingsService.cs`
  - Include `GitHubToken` in `GetSettings()`.

### WinUI

- Create `ai-dev.ui.winui/ViewModels/GitHubIssueImportViewModel.cs`
  - Dialog state, preview command, selection command, import command.
- Create `ai-dev.ui.winui/Views/Dialogs/GitHubIssueImportDialog.xaml.cs`
  - Programmatic `ContentDialog` matching current dialog style.
- Modify `ai-dev.ui.winui/ViewModels/BoardViewModel.cs`
  - Expose source metadata for task editing if needed by dialog binding.
  - Keep import execution out of this class.
- Modify `ai-dev.ui.winui/Views/Dialogs/TaskDialog.xaml.cs`
  - Show read-only external source row when task is imported.
- Modify `ai-dev.ui.winui/Views/Pages/BoardPage.xaml`
  - Add `Import from GitHub` button.
- Modify `ai-dev.ui.winui/Views/Pages/BoardPage.xaml.cs`
  - Open `GitHubIssueImportDialog`, refresh board after import.
- Modify `ai-dev.ui.winui/App.xaml.cs`
  - Register `GitHubIssueImportViewModel`.

### Tests

- Create `ai-dev-net.tests.unit/ExternalTaskSourceTests.cs`
- Modify `ai-dev-net.tests.unit/BoardTaskTests.cs`
- Create `ai-dev-net.tests.unit/BoardServiceImportTasksTests.cs`
- Create `ai-dev-net.tests.unit/GitHubRepositoryTests.cs`
- Create `ai-dev-net.tests.unit/GitHubRepositoryDetectorTests.cs`
- Create `ai-dev-net.tests.unit/GitHubIssuesClientTests.cs`
- Create `ai-dev-net.tests.unit/GitHubIssueImportServiceTests.cs`
- Modify `ai-dev-net.tests.unit/StudioSettingsServiceTests.cs`

---

## Implementation Tasks

### Task 1: External Task Source Metadata

**Files:**
- Create: `ai-dev.core/Features/Board/ExternalTaskSource.cs`
- Modify: `ai-dev.core/Features/Board/BoardTask.cs`
- Modify: `ai-dev.core/Features/Board/BoardService.cs`
- Test: `ai-dev-net.tests.unit/ExternalTaskSourceTests.cs`
- Test: `ai-dev-net.tests.unit/BoardTaskTests.cs`

- [ ] **Step 1: Write failing tests for `ExternalTaskSource`**

Create `ai-dev-net.tests.unit/ExternalTaskSourceTests.cs`:

```csharp
using System.Text.Json;

namespace AiDevNet.Tests.Unit;

public class ExternalTaskSourceTests
{
    [Fact]
    public void Constructor_NormalizesValues()
    {
        var source = new ExternalTaskSource(" GitHub ", " LLM-Dev-Studio/ai-dev#25 ", " https://github.com/LLM-Dev-Studio/ai-dev/issues/25 ");

        source.Provider.ShouldBe("github");
        source.Id.ShouldBe("LLM-Dev-Studio/ai-dev#25");
        source.Url.ShouldBe("https://github.com/LLM-Dev-Studio/ai-dev/issues/25");
    }

    [Theory]
    [InlineData(null, "id", "https://github.com/org/repo/issues/1")]
    [InlineData("", "id", "https://github.com/org/repo/issues/1")]
    [InlineData("github", null, "https://github.com/org/repo/issues/1")]
    [InlineData("github", "", "https://github.com/org/repo/issues/1")]
    [InlineData("github", "id", null)]
    [InlineData("github", "id", "")]
    [InlineData("github", "id", "not-a-url")]
    public void Constructor_InvalidValues_Throws(string? provider, string? id, string? url)
    {
        Should.Throw<ArgumentException>(() => new ExternalTaskSource(provider!, id!, url!));
    }

    [Fact]
    public void Matches_ComparesProviderCaseInsensitivelyAndIdOrdinally()
    {
        var source = new ExternalTaskSource("github", "LLM-Dev-Studio/ai-dev#25", "https://github.com/LLM-Dev-Studio/ai-dev/issues/25");

        source.Matches("GitHub", "LLM-Dev-Studio/ai-dev#25").ShouldBeTrue();
        source.Matches("github", "llm-dev-studio/ai-dev#25").ShouldBeFalse();
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var source = new ExternalTaskSource("github", "LLM-Dev-Studio/ai-dev#25", "https://github.com/LLM-Dev-Studio/ai-dev/issues/25");

        var json = JsonSerializer.Serialize(source, JsonDefaults.Write);
        var roundTripped = JsonSerializer.Deserialize<ExternalTaskSource>(json, JsonDefaults.Read);

        roundTripped.ShouldBe(source);
    }
}
```

- [ ] **Step 2: Run the new tests and verify they fail**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~ExternalTaskSourceTests"
```

Expected: compile fails because `ExternalTaskSource` does not exist.

- [ ] **Step 3: Implement `ExternalTaskSource`**

Create `ai-dev.core/Features/Board/ExternalTaskSource.cs`:

```csharp
namespace AiDev.Features.Board;

/// <summary>
/// Identifies an external system record that a board task was imported from.
/// </summary>
public sealed record ExternalTaskSource
{
    public ExternalTaskSource(string provider, string id, string url)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("External source provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("External source id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
            throw new ArgumentException("External source URL must be absolute.", nameof(url));

        Provider = provider.Trim().ToLowerInvariant();
        Id = id.Trim();
        Url = url.Trim();
    }

    public string Provider { get; }
    public string Id { get; }
    public string Url { get; }

    public bool Matches(string provider, string id)
        => string.Equals(Provider, provider?.Trim(), StringComparison.OrdinalIgnoreCase)
           && string.Equals(Id, id?.Trim(), StringComparison.Ordinal);
}
```

- [ ] **Step 4: Add `ExternalSource` to `BoardTask`**

Modify the `BoardTask` constructor and property:

```csharp
public BoardTask(
    TaskId id,
    string title,
    Priority? priority = null,
    string? description = null,
    string? assignee = null,
    List<string>? tags = null,
    DateTime? createdAt = null,
    DateTime? completedAt = null,
    DateTime? movedAt = null,
    DateTime? nudgedAt = null,
    ExternalTaskSource? externalSource = null)
{
    ArgumentNullException.ThrowIfNull(id);

    if (string.IsNullOrWhiteSpace(title))
        throw new ArgumentException("Task title is required.", nameof(title));

    Id = id;
    Title = title.Trim();
    Priority = NormalizePriority(priority);
    Description = NormalizeOptional(description);
    Assignee = NormalizeOptional(assignee);
    _tags = NormalizeTags(tags);
    CreatedAt = createdAt;
    CompletedAt = completedAt;
    MovedAt = movedAt;
    NudgedAt = nudgedAt;
    ExternalSource = externalSource;
}
```

Add the property after `NudgedAt`:

```csharp
/// <summary>External source record this task was imported from, when any.</summary>
public ExternalTaskSource? ExternalSource { get; }
```

- [ ] **Step 5: Persist external source metadata**

Modify `BoardTaskState` in `ai-dev.core/Features/Board/BoardService.cs`:

```csharp
internal sealed class BoardTaskState
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Priority { get; init; }
    public string? Description { get; init; }
    public string? Assignee { get; init; }
    public List<string>? Tags { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? MovedAt { get; init; }
    public DateTime? NudgedAt { get; init; }
    public ExternalTaskSource? ExternalSource { get; init; }
}
```

Add `ExternalSource` to serialization:

```csharp
ExternalSource = kv.Value.ExternalSource,
```

Add `externalSource` to deserialization:

```csharp
externalSource: taskState.ExternalSource);
```

- [ ] **Step 6: Add board task tests for source metadata**

Append to `ai-dev-net.tests.unit/BoardTaskTests.cs`:

```csharp
[Fact]
public void Constructor_WithExternalSource_StoresSource()
{
    var source = new ExternalTaskSource("github", "LLM-Dev-Studio/ai-dev#25", "https://github.com/LLM-Dev-Studio/ai-dev/issues/25");

    var task = new BoardTask(TaskId.New(), "Import issues", externalSource: source);

    task.ExternalSource.ShouldBe(source);
}
```

- [ ] **Step 7: Run tests**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~ExternalTaskSourceTests|FullyQualifiedName~BoardTaskTests"
```

Expected: all selected tests pass.

- [ ] **Step 8: Commit**

```bash
git add ai-dev.core/Features/Board/ExternalTaskSource.cs ai-dev.core/Features/Board/BoardTask.cs ai-dev.core/Features/Board/BoardService.cs ai-dev-net.tests.unit/ExternalTaskSourceTests.cs ai-dev-net.tests.unit/BoardTaskTests.cs
git commit -m "feat: track external board task sources"
```

### Task 2: Idempotent Bulk Board Import

**Files:**
- Create: `ai-dev.core/Features/Board/BoardTaskImportCandidate.cs`
- Create: `ai-dev.core/Features/Board/BoardTaskImportResult.cs`
- Modify: `ai-dev.core/Features/Board/IBoardService.cs`
- Modify: `ai-dev.core/Features/Board/BoardService.cs`
- Test: `ai-dev-net.tests.unit/BoardServiceImportTasksTests.cs`

- [ ] **Step 1: Write failing board import tests**

Create `ai-dev-net.tests.unit/BoardServiceImportTasksTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class BoardServiceImportTasksTests
{
    private static readonly ProjectSlug Project = new("github-import");

    [Fact]
    public async Task ImportTasksAsync_ImportsCandidatesIntoTargetColumn()
    {
        using var fixture = new BoardServiceFixture();
        var source = new ExternalTaskSource("github", "owner/repo#25", "https://github.com/owner/repo/issues/25");
        var candidate = new BoardTaskImportCandidate(
            "Import Issues From Github",
            "Body\n\nGitHub issue: https://github.com/owner/repo/issues/25",
            new DateTime(2026, 4, 11, 20, 3, 39, DateTimeKind.Utc),
            source);

        var result = await fixture.Service.ImportTasksAsync(Project, ColumnId.Backlog, [candidate], TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<BoardTaskImportResult>>().Value;
        ok.Imported.ShouldBe(1);
        ok.SkippedExisting.ShouldBe(0);
        ok.Failed.ShouldBe(0);

        var board = fixture.Service.LoadBoard(Project);
        var task = board.Tasks.Values.Single();
        task.Title.ShouldBe("Import Issues From Github");
        task.Description.ShouldBe("Body\n\nGitHub issue: https://github.com/owner/repo/issues/25");
        task.Priority.ShouldBe(Priority.Normal);
        task.Assignee.ShouldBeNull();
        task.Tags.ShouldBeEmpty();
        task.CreatedAt.ShouldBe(candidate.CreatedAt);
        task.MovedAt.ShouldNotBeNull();
        task.ExternalSource.ShouldBe(source);
        board.Columns.Single(c => c.Id == ColumnId.Backlog).TaskIds.ShouldContain(task.Id);
    }

    [Fact]
    public async Task ImportTasksAsync_SkipsExistingExternalSource()
    {
        using var fixture = new BoardServiceFixture();
        var source = new ExternalTaskSource("github", "owner/repo#25", "https://github.com/owner/repo/issues/25");
        var candidate = new BoardTaskImportCandidate("First title", "First body", DateTime.UtcNow, source);

        await fixture.Service.ImportTasksAsync(Project, ColumnId.Backlog, [candidate], TestContext.Current.CancellationToken);
        var second = await fixture.Service.ImportTasksAsync(Project, ColumnId.Backlog, [candidate with { Title = "Changed title" }], TestContext.Current.CancellationToken);

        var ok = second.ShouldBeOfType<Ok<BoardTaskImportResult>>().Value;
        ok.Imported.ShouldBe(0);
        ok.SkippedExisting.ShouldBe(1);
        ok.Failed.ShouldBe(0);
        fixture.Service.LoadBoard(Project).Tasks.Count.ShouldBe(1);
        fixture.Service.LoadBoard(Project).Tasks.Values.Single().Title.ShouldBe("First title");
    }

    [Fact]
    public async Task ImportTasksAsync_UnknownColumn_ReturnsError()
    {
        using var fixture = new BoardServiceFixture();
        var candidate = new BoardTaskImportCandidate(
            "Title",
            "GitHub issue: https://github.com/owner/repo/issues/25",
            DateTime.UtcNow,
            new ExternalTaskSource("github", "owner/repo#25", "https://github.com/owner/repo/issues/25"));

        var result = await fixture.Service.ImportTasksAsync(Project, new ColumnId("triage"), [candidate], TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<BoardTaskImportResult>>();
        err.Error.Code.ShouldBe("BOARD_UNKNOWN_COLUMN");
    }

    private sealed class BoardServiceFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-dev-board-import-tests", Guid.NewGuid().ToString("N"));

        public BoardService Service { get; }

        public BoardServiceFixture()
        {
            Directory.CreateDirectory(_root);
            var paths = new WorkspacePaths(new RootDir(_root));
            Service = new BoardService(
                paths,
                CreatePassingDispatcher(),
                new AtomicFileWriter(),
                new ProjectMutationCoordinator(),
                NullLogger<BoardService>.Instance,
                new ProjectStateChangedNotifier());
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private static IDomainEventDispatcher CreatePassingDispatcher()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        dispatcher.Dispatch(Arg.Any<IReadOnlyList<DomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<Unit>(Unit.Value));
        return dispatcher;
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~BoardServiceImportTasksTests"
```

Expected: compile fails because import records and `ImportTasksAsync` do not exist.

- [ ] **Step 3: Add import records**

Create `ai-dev.core/Features/Board/BoardTaskImportCandidate.cs`:

```csharp
namespace AiDev.Features.Board;

public sealed record BoardTaskImportCandidate(
    string Title,
    string? Description,
    DateTime? CreatedAt,
    ExternalTaskSource ExternalSource);
```

Create `ai-dev.core/Features/Board/BoardTaskImportResult.cs`:

```csharp
namespace AiDev.Features.Board;

public enum BoardTaskImportItemStatus
{
    Imported,
    SkippedExisting,
    Failed
}

public sealed record BoardTaskImportItemResult(
    string ExternalId,
    string Title,
    BoardTaskImportItemStatus Status,
    TaskId? TaskId,
    string? Reason);

public sealed record BoardTaskImportResult(
    int Imported,
    int SkippedExisting,
    int Failed,
    IReadOnlyList<BoardTaskImportItemResult> Items);
```

- [ ] **Step 4: Add `IBoardService.ImportTasksAsync`**

Add to `ai-dev.core/Features/Board/IBoardService.cs`:

```csharp
/// <summary>
/// Imports externally sourced tasks into a board column, skipping tasks whose external source already exists.
/// </summary>
Task<Result<BoardTaskImportResult>> ImportTasksAsync(ProjectSlug projectSlug, ColumnId columnId,
    IReadOnlyList<BoardTaskImportCandidate> candidates, CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement `BoardService.ImportTasksAsync`**

Add this method to `BoardService`:

```csharp
public Task<Result<BoardTaskImportResult>> ImportTasksAsync(ProjectSlug projectSlug, ColumnId columnId,
    IReadOnlyList<BoardTaskImportCandidate> candidates, CancellationToken cancellationToken = default)
    => coordinator.ExecuteAsync(projectSlug, async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var board = LoadBoard(projectSlug);
        if (!board.Columns.Any(c => c.Id == columnId))
            return new Err<BoardTaskImportResult>(new DomainError("BOARD_UNKNOWN_COLUMN", "Column not found."));

        var items = new List<BoardTaskImportItemResult>();
        var imported = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (board.Tasks.Values.Any(task => task.ExternalSource?.Matches(candidate.ExternalSource.Provider, candidate.ExternalSource.Id) == true))
            {
                skipped++;
                items.Add(new(candidate.ExternalSource.Id, candidate.Title, BoardTaskImportItemStatus.SkippedExisting, null, "Already imported."));
                continue;
            }

            BoardTask task;
            try
            {
                task = new BoardTask(
                    TaskId.New(),
                    candidate.Title,
                    Priority.Normal,
                    candidate.Description,
                    assignee: null,
                    tags: null,
                    createdAt: candidate.CreatedAt,
                    movedAt: DateTime.UtcNow,
                    externalSource: candidate.ExternalSource);
            }
            catch (ArgumentException ex)
            {
                failed++;
                items.Add(new(candidate.ExternalSource.Id, candidate.Title, BoardTaskImportItemStatus.Failed, null, ex.Message));
                continue;
            }

            var addResult = board.AddTask(columnId, task);
            if (addResult is Ok<BoardTask>)
            {
                imported++;
                items.Add(new(candidate.ExternalSource.Id, candidate.Title, BoardTaskImportItemStatus.Imported, task.Id, null));
            }
            else if (addResult is Err<BoardTask> err)
            {
                failed++;
                items.Add(new(candidate.ExternalSource.Id, candidate.Title, BoardTaskImportItemStatus.Failed, null, err.Error.Message));
            }
        }

        if (imported > 0)
        {
            SaveBoard(projectSlug, board);
            var dispatchResult = await DispatchBoardEventsAsync(board.DequeueDomainEvents()).ConfigureAwait(false);
            if (dispatchResult is Err<Unit> err)
                return new Err<BoardTaskImportResult>(err.Error);
        }

        return new Ok<BoardTaskImportResult>(new BoardTaskImportResult(imported, skipped, failed, items));
    }, cancellationToken);
```

- [ ] **Step 6: Run board import tests**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~BoardServiceImportTasksTests"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```bash
git add ai-dev.core/Features/Board/BoardTaskImportCandidate.cs ai-dev.core/Features/Board/BoardTaskImportResult.cs ai-dev.core/Features/Board/IBoardService.cs ai-dev.core/Features/Board/BoardService.cs ai-dev-net.tests.unit/BoardServiceImportTasksTests.cs
git commit -m "feat: import external board tasks idempotently"
```

### Task 3: GitHub Repository Parsing And Detection

**Files:**
- Create: `ai-dev.core/Features/GitHub/GitHubRepository.cs`
- Create: `ai-dev.core/Features/GitHub/GitHubRepositoryDetector.cs`
- Test: `ai-dev-net.tests.unit/GitHubRepositoryTests.cs`
- Test: `ai-dev-net.tests.unit/GitHubRepositoryDetectorTests.cs`

- [ ] **Step 1: Write failing repository tests**

Create `ai-dev-net.tests.unit/GitHubRepositoryTests.cs`:

```csharp
using AiDev.Features.GitHub;

namespace AiDevNet.Tests.Unit;

public class GitHubRepositoryTests
{
    [Fact]
    public void Parse_OwnerRepo_ReturnsRepository()
    {
        var repo = GitHubRepository.Parse(" LLM-Dev-Studio/ai-dev ");

        repo.Owner.ShouldBe("LLM-Dev-Studio");
        repo.Name.ShouldBe("ai-dev");
        repo.FullName.ShouldBe("LLM-Dev-Studio/ai-dev");
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner/repo/extra")]
    [InlineData("https://github.com/owner/repo")]
    public void TryParse_InvalidInput_ReturnsFalse(string value)
    {
        GitHubRepository.TryParse(value, out _).ShouldBeFalse();
    }
}
```

Create `ai-dev-net.tests.unit/GitHubRepositoryDetectorTests.cs`:

```csharp
using AiDev.Features.GitHub;

namespace AiDevNet.Tests.Unit;

public class GitHubRepositoryDetectorTests
{
    [Fact]
    public void TryDetect_PrefersUpstreamOverOrigin()
    {
        using var fixture = new GitConfigFixture("""
            [remote "origin"]
                url = https://github.com/fork/ai-dev.git
            [remote "upstream"]
                url = git@github.com:LLM-Dev-Studio/ai-dev.git
            """);

        var result = GitHubRepositoryDetector.TryDetect(fixture.Root);

        result.ShouldNotBeNull();
        result.FullName.ShouldBe("LLM-Dev-Studio/ai-dev");
    }

    [Fact]
    public void TryDetect_NoGitConfig_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "ai-dev-github-detect-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            GitHubRepositoryDetector.TryDetect(root).ShouldBeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class GitConfigFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ai-dev-github-detect-tests", Guid.NewGuid().ToString("N"));

        public GitConfigFixture(string config)
        {
            var gitDir = Path.Combine(Root, ".git");
            Directory.CreateDirectory(gitDir);
            File.WriteAllText(Path.Combine(gitDir, "config"), config);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~GitHubRepositoryTests|FullyQualifiedName~GitHubRepositoryDetectorTests"
```

Expected: compile fails because GitHub repository types do not exist.

- [ ] **Step 3: Implement repository parsing**

Create `ai-dev.core/Features/GitHub/GitHubRepository.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace AiDev.Features.GitHub;

public sealed record GitHubRepository(string Owner, string Name)
{
    public string FullName => $"{Owner}/{Name}";

    public static GitHubRepository Parse(string value)
        => TryParse(value, out var repo)
            ? repo
            : throw new ArgumentException("Repository must be in owner/repo format.", nameof(value));

    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out GitHubRepository? repository)
    {
        repository = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;
        if (parts[0].Length == 0 || parts[1].Length == 0)
            return false;
        if (parts[0].Contains(':', StringComparison.Ordinal) || parts[1].Contains(':', StringComparison.Ordinal))
            return false;

        repository = new GitHubRepository(parts[0], parts[1]);
        return true;
    }
}
```

- [ ] **Step 4: Implement repository detector**

Create `ai-dev.core/Features/GitHub/GitHubRepositoryDetector.cs`:

```csharp
using System.Text.RegularExpressions;

namespace AiDev.Features.GitHub;

public static partial class GitHubRepositoryDetector
{
    public static GitHubRepository? TryDetect(string? codebasePath)
    {
        if (string.IsNullOrWhiteSpace(codebasePath))
            return null;

        var configPath = Path.Combine(codebasePath, ".git", "config");
        if (!File.Exists(configPath))
            return null;

        var raw = File.ReadAllText(configPath);
        var remotes = new Dictionary<string, GitHubRepository>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RemotePattern().Matches(raw))
        {
            var name = match.Groups["name"].Value.Trim();
            var url = match.Groups["url"].Value.Trim();
            var repo = ParseRemoteUrl(url);
            if (repo != null)
                remotes[name] = repo;
        }

        if (remotes.TryGetValue("upstream", out var upstream))
            return upstream;
        if (remotes.TryGetValue("origin", out var origin))
            return origin;

        return remotes.Values.FirstOrDefault();
    }

    private static GitHubRepository? ParseRemoteUrl(string url)
    {
        var match = GitHubUrlPattern().Match(url);
        if (!match.Success)
            return null;

        return new GitHubRepository(match.Groups["owner"].Value, match.Groups["repo"].Value);
    }

    [GeneratedRegex("\\[remote\\s+\"(?<name>[^\"]+)\"\\][\\s\\S]*?url\\s*=\\s*(?<url>[^\\r\\n]+)", RegexOptions.Compiled)]
    private static partial Regex RemotePattern();

    [GeneratedRegex("github\\.com[/:](?<owner>[^/\\s]+)/(?<repo>[^/\\s.]+?)(?:\\.git)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GitHubUrlPattern();
}
```

- [ ] **Step 5: Run repository tests**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~GitHubRepositoryTests|FullyQualifiedName~GitHubRepositoryDetectorTests"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```bash
git add ai-dev.core/Features/GitHub/GitHubRepository.cs ai-dev.core/Features/GitHub/GitHubRepositoryDetector.cs ai-dev-net.tests.unit/GitHubRepositoryTests.cs ai-dev-net.tests.unit/GitHubRepositoryDetectorTests.cs
git commit -m "feat: detect github repositories"
```

### Task 4: GitHub Issues Client

**Files:**
- Modify: `ai-dev.core/ai-dev.core.csproj`
- Modify: `ai-dev.core/Services/StudioSettingsService.cs`
- Create: `ai-dev.core/Features/GitHub/GitHubIssueSummary.cs`
- Create: `ai-dev.core/Features/GitHub/IGitHubIssuesClient.cs`
- Create: `ai-dev.core/Features/GitHub/GitHubIssuesClient.cs`
- Modify: `ai-dev.core/Extensions/CoreServiceExtensions.cs`
- Test: `ai-dev-net.tests.unit/GitHubIssuesClientTests.cs`
- Test: `ai-dev-net.tests.unit/StudioSettingsServiceTests.cs`

- [ ] **Step 1: Write failing GitHub client tests**

Create `ai-dev-net.tests.unit/GitHubIssuesClientTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using AiDev.Features.GitHub;
using System.Net;

namespace AiDevNet.Tests.Unit;

public class GitHubIssuesClientTests
{
    [Fact]
    public async Task ListOpenIssuesAsync_WhenTokenMissing_ReturnsError()
    {
        var client = CreateClient(token: null, new QueueHttpMessageHandler());

        var result = await client.ListOpenIssuesAsync(new GitHubRepository("owner", "repo"), null, 500, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<IReadOnlyList<GitHubIssueSummary>>>();
        err.Error.Code.ShouldBe("GITHUB_TOKEN_MISSING");
    }

    [Fact]
    public async Task ListOpenIssuesAsync_PaginatesAndFiltersPullRequests()
    {
        var handler = new QueueHttpMessageHandler();
        handler.EnqueueJson("""
            [
              { "number": 1, "title": "First", "body": "Body", "html_url": "https://github.com/owner/repo/issues/1", "created_at": "2026-04-11T20:03:39Z", "labels": [{ "name": "ready" }] },
              { "number": 2, "title": "PR", "body": "Body", "html_url": "https://github.com/owner/repo/pull/2", "created_at": "2026-04-11T20:03:39Z", "labels": [], "pull_request": {} }
            ]
            """);
        handler.EnqueueJson("[]");
        var client = CreateClient("token", handler);

        var result = await client.ListOpenIssuesAsync(new GitHubRepository("owner", "repo"), "ready", 500, TestContext.Current.CancellationToken);

        var issues = result.ShouldBeOfType<Ok<IReadOnlyList<GitHubIssueSummary>>>().Value;
        issues.Count.ShouldBe(1);
        issues[0].Number.ShouldBe(1);
        issues[0].Labels.ShouldBe(["ready"]);
        handler.Requests[0].RequestUri!.Query.ShouldContain("state=open");
        handler.Requests[0].RequestUri!.Query.ShouldContain("labels=ready");
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
    }

    [Fact]
    public async Task ListOpenIssuesAsync_RateLimited_ReturnsTypedError()
    {
        var handler = new QueueHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"message":"API rate limit exceeded"}""")
        });
        var client = CreateClient("token", handler);

        var result = await client.ListOpenIssuesAsync(new GitHubRepository("owner", "repo"), null, 500, TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<IReadOnlyList<GitHubIssueSummary>>>();
        err.Error.Code.ShouldBe("GITHUB_RATE_LIMITED");
    }

    private static GitHubIssuesClient CreateClient(string? token, HttpMessageHandler handler)
    {
        var values = token == null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["StudioSettings:GitHubToken"] = token };
        var settings = new StudioSettingsService(new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build());

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("github-issues").Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") });
        return new GitHubIssuesClient(factory, settings);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = [];

        public void EnqueueJson(string json)
            => Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No queued response.");
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
```

- [ ] **Step 2: Add settings test for GitHub token loading**

Append to `ai-dev-net.tests.unit/StudioSettingsServiceTests.cs`:

```csharp
[Fact]
public void GetSettings_ReadsGitHubTokenFromStudioSettingsSection()
{
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StudioSettings:GitHubToken"] = " ghp_example "
        })
        .Build();
    var service = new StudioSettingsService(config);

    var settings = service.GetSettings();

    settings.GitHubToken.ShouldBe("ghp_example");
}
```

- [ ] **Step 3: Run tests and verify they fail**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~GitHubIssuesClientTests|FullyQualifiedName~StudioSettingsServiceTests.GetSettings_ReadsGitHubTokenFromStudioSettingsSection"
```

Expected: compile fails because GitHub client types do not exist, and the settings test fails until `GetSettings()` reads `GitHubToken`.

- [ ] **Step 4: Add package reference**

Modify `ai-dev.core/ai-dev.core.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="10.0.6" />
```

- [ ] **Step 5: Fix `StudioSettingsService.GetSettings()`**

In `ai-dev.core/Services/StudioSettingsService.cs`, read the token:

```csharp
var gitHubToken = GetConfiguredValue(nameof(StudioSettings.GitHubToken));
```

Add it to the returned settings:

```csharp
GitHubToken = string.IsNullOrWhiteSpace(gitHubToken) ? null : gitHubToken.Trim(),
```

- [ ] **Step 6: Add GitHub issue DTO and interface**

Create `ai-dev.core/Features/GitHub/GitHubIssueSummary.cs`:

```csharp
namespace AiDev.Features.GitHub;

public sealed record GitHubIssueSummary(
    int Number,
    string Title,
    string? Body,
    string Url,
    DateTime CreatedAt,
    IReadOnlyList<string> Labels);
```

Create `ai-dev.core/Features/GitHub/IGitHubIssuesClient.cs`:

```csharp
namespace AiDev.Features.GitHub;

public interface IGitHubIssuesClient
{
    Task<Result<IReadOnlyList<GitHubIssueSummary>>> ListOpenIssuesAsync(
        GitHubRepository repository,
        string? label,
        int cap,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 7: Implement GitHub issues client**

Create `ai-dev.core/Features/GitHub/GitHubIssuesClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiDev.Services;

namespace AiDev.Features.GitHub;

public sealed class GitHubIssuesClient(IHttpClientFactory httpClientFactory, StudioSettingsService settingsService) : IGitHubIssuesClient
{
    private const int PageSize = 100;

    public async Task<Result<IReadOnlyList<GitHubIssueSummary>>> ListOpenIssuesAsync(
        GitHubRepository repository,
        string? label,
        int cap,
        CancellationToken cancellationToken = default)
    {
        var token = settingsService.GetSettings().GitHubToken;
        if (string.IsNullOrWhiteSpace(token))
            return new Err<IReadOnlyList<GitHubIssueSummary>>(new DomainError("GITHUB_TOKEN_MISSING", "Add a GitHub token in Settings to import issues."));

        var client = httpClientFactory.CreateClient("github-issues");
        var issues = new List<GitHubIssueSummary>();
        for (var page = 1; issues.Count < cap; page++)
        {
            var path = $"/repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}/issues?state=open&per_page={PageSize}&page={page}";
            if (!string.IsNullOrWhiteSpace(label))
                path += $"&labels={Uri.EscapeDataString(label.Trim())}";

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.UserAgent.ParseAdd("ai-dev-studio");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return await ToErrorAsync(response).ConfigureAwait(false);

            var payload = await response.Content.ReadFromJsonAsync<List<GitHubIssueResponse>>(JsonDefaults.Read, cancellationToken).ConfigureAwait(false) ?? [];
            foreach (var item in payload)
            {
                if (item.PullRequest != null)
                    continue;
                issues.Add(new GitHubIssueSummary(
                    item.Number,
                    item.Title ?? $"Issue #{item.Number}",
                    item.Body,
                    item.HtmlUrl ?? $"https://github.com/{repository.FullName}/issues/{item.Number}",
                    item.CreatedAt,
                    [.. item.Labels.Select(l => l.Name).Where(name => !string.IsNullOrWhiteSpace(name))]));
                if (issues.Count == cap)
                    break;
            }

            if (payload.Count < PageSize)
                break;
        }

        return new Ok<IReadOnlyList<GitHubIssueSummary>>(issues);
    }

    private static async Task<Result<IReadOnlyList<GitHubIssueSummary>>> ToErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var code = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "GITHUB_UNAUTHORIZED",
            HttpStatusCode.Forbidden when text.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => "GITHUB_RATE_LIMITED",
            HttpStatusCode.Forbidden => "GITHUB_FORBIDDEN",
            HttpStatusCode.NotFound => "GITHUB_REPOSITORY_NOT_FOUND",
            _ => "GITHUB_REQUEST_FAILED"
        };

        return new Err<IReadOnlyList<GitHubIssueSummary>>(new DomainError(code, $"GitHub returned {(int)response.StatusCode}: {text}"));
    }

    private sealed class GitHubIssueResponse
    {
        [JsonPropertyName("number")] public int Number { get; init; }
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("body")] public string? Body { get; init; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; init; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
        [JsonPropertyName("labels")] public List<GitHubLabelResponse> Labels { get; init; } = [];
        [JsonPropertyName("pull_request")] public object? PullRequest { get; init; }
    }

    private sealed class GitHubLabelResponse
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
    }
}
```

- [ ] **Step 8: Register GitHub client**

Modify `ai-dev.core/Extensions/CoreServiceExtensions.cs`:

```csharp
using AiDev.Features.GitHub;
```

Add in `AddAiDevCore`:

```csharp
services.AddHttpClient("github-issues", client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
});
services.AddSingleton<IGitHubIssuesClient, GitHubIssuesClient>();
```

- [ ] **Step 9: Run tests**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~GitHubIssuesClientTests|FullyQualifiedName~StudioSettingsServiceTests.GetSettings_ReadsGitHubTokenFromStudioSettingsSection"
```

Expected: all selected tests pass.

- [ ] **Step 10: Commit**

```bash
git add ai-dev.core/ai-dev.core.csproj ai-dev.core/Services/StudioSettingsService.cs ai-dev.core/Features/GitHub/GitHubIssueSummary.cs ai-dev.core/Features/GitHub/IGitHubIssuesClient.cs ai-dev.core/Features/GitHub/GitHubIssuesClient.cs ai-dev.core/Extensions/CoreServiceExtensions.cs ai-dev-net.tests.unit/GitHubIssuesClientTests.cs ai-dev-net.tests.unit/StudioSettingsServiceTests.cs
git commit -m "feat: add github issues client"
```

### Task 5: GitHub Issue Import Service

**Files:**
- Create: `ai-dev.core/Features/GitHub/GitHubIssueImportModels.cs`
- Create: `ai-dev.core/Features/GitHub/GitHubIssueImportService.cs`
- Modify: `ai-dev.core/Extensions/CoreServiceExtensions.cs`
- Test: `ai-dev-net.tests.unit/GitHubIssueImportServiceTests.cs`

- [ ] **Step 1: Write failing import service tests**

Create `ai-dev-net.tests.unit/GitHubIssueImportServiceTests.cs`:

```csharp
using AiDev.Features.GitHub;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class GitHubIssueImportServiceTests
{
    private static readonly ProjectSlug Project = new("github-import");

    [Fact]
    public async Task PreviewAsync_MarksAlreadyImportedIssues()
    {
        using var fixture = new ImportFixture([
            new GitHubIssueSummary(25, "Import Issues From Github", "Body", "https://github.com/owner/repo/issues/25", DateTime.UtcNow, ["ready"])
        ]);
        await fixture.BoardService.ImportTasksAsync(Project, ColumnId.Backlog, [
            new BoardTaskImportCandidate(
                "Existing",
                "Existing",
                DateTime.UtcNow,
                new ExternalTaskSource("github", "owner/repo#25", "https://github.com/owner/repo/issues/25"))
        ], TestContext.Current.CancellationToken);

        var result = await fixture.Service.PreviewAsync(Project, "owner/repo", "ready", TestContext.Current.CancellationToken);

        var preview = result.ShouldBeOfType<Ok<GitHubIssueImportPreview>>().Value;
        preview.Issues.Single().AlreadyImported.ShouldBeTrue();
        preview.Issues.Single().Selected.ShouldBeFalse();
        preview.Issues.Single().Labels.ShouldBe(["ready"]);
    }

    [Fact]
    public async Task ImportAsync_ImportsSelectedOpenIssuesWithoutTags()
    {
        using var fixture = new ImportFixture([
            new GitHubIssueSummary(25, "Import Issues From Github", "Body", "https://github.com/owner/repo/issues/25", new DateTime(2026, 4, 11, 20, 3, 39, DateTimeKind.Utc), ["ready"])
        ]);

        var result = await fixture.Service.ImportAsync(Project, new GitHubIssueImportRequest(
            Repository: "owner/repo",
            Label: "ready",
            TargetColumnId: ColumnId.Backlog.Value,
            SelectedIssueNumbers: [25]), TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<GitHubIssueImportResult>>().Value;
        ok.BoardResult.Imported.ShouldBe(1);
        var task = fixture.BoardService.LoadBoard(Project).Tasks.Values.Single();
        task.Tags.ShouldBeEmpty();
        task.Description.ShouldBe("Body\n\nGitHub issue: https://github.com/owner/repo/issues/25");
        task.ExternalSource!.Id.ShouldBe("owner/repo#25");
    }

    private sealed class ImportFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ai-dev-github-import-service-tests", Guid.NewGuid().ToString("N"));

        public BoardService BoardService { get; }
        public GitHubIssueImportService Service { get; }

        public ImportFixture(IReadOnlyList<GitHubIssueSummary> issues)
        {
            Directory.CreateDirectory(_root);
            var paths = new WorkspacePaths(new RootDir(_root));
            BoardService = new BoardService(paths, CreatePassingDispatcher(), new AtomicFileWriter(), new ProjectMutationCoordinator(), NullLogger<BoardService>.Instance, new ProjectStateChangedNotifier());
            Service = new GitHubIssueImportService(new StubGitHubIssuesClient(issues), BoardService);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private static IDomainEventDispatcher CreatePassingDispatcher()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        dispatcher.Dispatch(Arg.Any<IReadOnlyList<DomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<Unit>(Unit.Value));
        return dispatcher;
    }

    private sealed class StubGitHubIssuesClient(IReadOnlyList<GitHubIssueSummary> issues) : IGitHubIssuesClient
    {
        public Task<Result<IReadOnlyList<GitHubIssueSummary>>> ListOpenIssuesAsync(GitHubRepository repository, string? label, int cap, CancellationToken cancellationToken = default)
            => Task.FromResult<Result<IReadOnlyList<GitHubIssueSummary>>>(new Ok<IReadOnlyList<GitHubIssueSummary>>(issues));
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~GitHubIssueImportServiceTests"
```

Expected: compile fails because import service models do not exist.

- [ ] **Step 3: Add import models**

Create `ai-dev.core/Features/GitHub/GitHubIssueImportModels.cs`:

```csharp
namespace AiDev.Features.GitHub;

public sealed record GitHubIssueImportPreviewItem(
    int Number,
    string Title,
    string Url,
    DateTime CreatedAt,
    IReadOnlyList<string> Labels,
    bool AlreadyImported,
    bool Selected);

public sealed record GitHubIssueImportPreview(
    string Repository,
    string? Label,
    bool CapReached,
    IReadOnlyList<GitHubIssueImportPreviewItem> Issues);

public sealed record GitHubIssueImportRequest(
    string Repository,
    string? Label,
    string TargetColumnId,
    IReadOnlyList<int> SelectedIssueNumbers);

public sealed record GitHubIssueImportResult(
    string Repository,
    BoardTaskImportResult BoardResult);
```

- [ ] **Step 4: Implement import service**

Create `ai-dev.core/Features/GitHub/GitHubIssueImportService.cs`:

```csharp
using AiDev.Features.Board;

namespace AiDev.Features.GitHub;

public sealed class GitHubIssueImportService(IGitHubIssuesClient gitHubIssuesClient, IBoardService boardService)
{
    private const int IssueCap = 500;
    private const string Provider = "github";

    public async Task<Result<GitHubIssueImportPreview>> PreviewAsync(
        ProjectSlug projectSlug,
        string repositoryInput,
        string? label,
        CancellationToken cancellationToken = default)
    {
        if (!GitHubRepository.TryParse(repositoryInput, out var repository))
            return new Err<GitHubIssueImportPreview>(new DomainError("GITHUB_INVALID_REPOSITORY", "Repository must be in owner/repo format."));

        var issuesResult = await gitHubIssuesClient.ListOpenIssuesAsync(repository, label, IssueCap, cancellationToken).ConfigureAwait(false);
        if (issuesResult is Err<IReadOnlyList<GitHubIssueSummary>> issuesErr)
            return new Err<GitHubIssueImportPreview>(issuesErr.Error);

        var issues = ((Ok<IReadOnlyList<GitHubIssueSummary>>)issuesResult).Value;
        var imported = GetImportedExternalIds(projectSlug);
        var previewItems = issues.Select(issue =>
        {
            var externalId = ExternalId(repository, issue.Number);
            var alreadyImported = imported.Contains(externalId);
            return new GitHubIssueImportPreviewItem(
                issue.Number,
                issue.Title,
                issue.Url,
                issue.CreatedAt,
                issue.Labels,
                alreadyImported,
                Selected: !alreadyImported);
        }).ToList();

        return new Ok<GitHubIssueImportPreview>(new GitHubIssueImportPreview(repository.FullName, NormalizeLabel(label), issues.Count >= IssueCap, previewItems));
    }

    public async Task<Result<GitHubIssueImportResult>> ImportAsync(
        ProjectSlug projectSlug,
        GitHubIssueImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!GitHubRepository.TryParse(request.Repository, out var repository))
            return new Err<GitHubIssueImportResult>(new DomainError("GITHUB_INVALID_REPOSITORY", "Repository must be in owner/repo format."));
        if (!ColumnId.TryParse(request.TargetColumnId, out var columnId))
            return new Err<GitHubIssueImportResult>(new DomainError("BOARD_INVALID_COLUMN", "Column id is invalid."));

        var issuesResult = await gitHubIssuesClient.ListOpenIssuesAsync(repository, request.Label, IssueCap, cancellationToken).ConfigureAwait(false);
        if (issuesResult is Err<IReadOnlyList<GitHubIssueSummary>> issuesErr)
            return new Err<GitHubIssueImportResult>(issuesErr.Error);

        var selected = request.SelectedIssueNumbers.ToHashSet();
        var candidates = ((Ok<IReadOnlyList<GitHubIssueSummary>>)issuesResult).Value
            .Where(issue => selected.Contains(issue.Number))
            .Select(issue => ToCandidate(repository, issue))
            .ToList();

        var importResult = await boardService.ImportTasksAsync(projectSlug, columnId, candidates, cancellationToken).ConfigureAwait(false);
        return importResult switch
        {
            Ok<BoardTaskImportResult> ok => new Ok<GitHubIssueImportResult>(new GitHubIssueImportResult(repository.FullName, ok.Value)),
            Err<BoardTaskImportResult> err => new Err<GitHubIssueImportResult>(err.Error),
            _ => new Err<GitHubIssueImportResult>(new DomainError("GITHUB_IMPORT_FAILED", "Unexpected import result."))
        };
    }

    private HashSet<string> GetImportedExternalIds(ProjectSlug projectSlug)
        => boardService.LoadBoard(projectSlug).Tasks.Values
            .Where(task => string.Equals(task.ExternalSource?.Provider, Provider, StringComparison.OrdinalIgnoreCase))
            .Select(task => task.ExternalSource!.Id)
            .ToHashSet(StringComparer.Ordinal);

    private static BoardTaskImportCandidate ToCandidate(GitHubRepository repository, GitHubIssueSummary issue)
        => new(
            issue.Title,
            BuildDescription(issue.Body, issue.Url),
            issue.CreatedAt,
            new ExternalTaskSource(Provider, ExternalId(repository, issue.Number), issue.Url));

    private static string ExternalId(GitHubRepository repository, int issueNumber)
        => $"{repository.FullName}#{issueNumber}";

    private static string BuildDescription(string? body, string url)
    {
        var sourceLine = $"GitHub issue: {url}";
        return string.IsNullOrWhiteSpace(body)
            ? sourceLine
            : $"{body.TrimEnd()}\n\n{sourceLine}";
    }

    private static string? NormalizeLabel(string? label)
        => string.IsNullOrWhiteSpace(label) ? null : label.Trim();
}
```

- [ ] **Step 5: Register import service**

Modify `ai-dev.core/Extensions/CoreServiceExtensions.cs`:

```csharp
services.AddSingleton<GitHubIssueImportService>();
```

- [ ] **Step 6: Run import service tests**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~GitHubIssueImportServiceTests"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```bash
git add ai-dev.core/Features/GitHub/GitHubIssueImportModels.cs ai-dev.core/Features/GitHub/GitHubIssueImportService.cs ai-dev.core/Extensions/CoreServiceExtensions.cs ai-dev-net.tests.unit/GitHubIssueImportServiceTests.cs
git commit -m "feat: preview and import github issues"
```

### Task 6: WinUI Import Dialog View Model

**Files:**
- Create: `ai-dev.ui.winui/ViewModels/GitHubIssueImportViewModel.cs`
- Modify: `ai-dev.ui.winui/App.xaml.cs`

- [ ] **Step 1: Add the WinUI import view model**

Create `ai-dev.ui.winui/ViewModels/GitHubIssueImportViewModel.cs`:

```csharp
using AiDev.Features.GitHub;
using AiDev.Features.Workspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AiDev.WinUI.ViewModels;

public partial class GitHubIssueImportItemViewModel(GitHubIssueImportPreviewItem item) : ObservableObject
{
    public int Number { get; } = item.Number;
    public string IssueNumberText => $"#{Number}";
    public string Title { get; } = item.Title;
    public string Url { get; } = item.Url;
    public DateTime CreatedAt { get; } = item.CreatedAt;
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd");
    public string LabelsText { get; } = item.Labels.Count == 0 ? "" : string.Join(", ", item.Labels);
    public bool AlreadyImported { get; } = item.AlreadyImported;
    public bool CanSelect => !AlreadyImported;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = item.Selected;

    public string StatusText => AlreadyImported ? "Already imported" : "Ready";
}

public sealed record ImportColumnOption(string Title, string Id);

public partial class GitHubIssueImportViewModel(
    GitHubIssueImportService importService,
    IBoardService boardService,
    MainViewModel mainViewModel,
    ActiveWorkspaceHolder activeWorkspace) : ObservableObject
{
    [ObservableProperty] public partial string Repository { get; set; } = "";
    [ObservableProperty] public partial string Label { get; set; } = "";
    [ObservableProperty] public partial ImportColumnOption? SelectedColumn { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial string ResultMessage { get; set; } = "";
    [ObservableProperty] public partial bool HasPreview { get; set; }
    [ObservableProperty] public partial int ItemsImported { get; set; }

    public ObservableCollection<ImportColumnOption> Columns { get; } = [];
    public ObservableCollection<GitHubIssueImportItemViewModel> Issues { get; } = [];
    public ObservableCollection<string> ImportResults { get; } = [];

    public ProjectSlug? CurrentSlug => mainViewModel.ActiveProject?.Slug;

    public void Initialize()
    {
        Repository = GitHubRepositoryDetector.TryDetect(activeWorkspace.ActiveCodebasePath)?.FullName ?? "";
        RefreshColumns();
        Issues.Clear();
        ImportResults.Clear();
        HasPreview = false;
        ErrorMessage = "";
        ResultMessage = "";
        ItemsImported = 0;
    }

    [RelayCommand]
    public async Task PreviewAsync()
    {
        if (CurrentSlug is null)
            return;

        IsBusy = true;
        ErrorMessage = "";
        ResultMessage = "";
        ImportResults.Clear();
        ItemsImported = 0;
        try
        {
            var result = await importService.PreviewAsync(CurrentSlug, Repository, NormalizeLabel(Label));
            if (result is Err<GitHubIssueImportPreview> err)
            {
                ErrorMessage = err.Error.Message;
                return;
            }

            var preview = ((Ok<GitHubIssueImportPreview>)result).Value;
            Issues.Clear();
            foreach (var issue in preview.Issues)
                Issues.Add(new GitHubIssueImportItemViewModel(issue));

            HasPreview = true;
            ResultMessage = preview.CapReached
                ? "Showing the first 500 open issues. Narrow the label filter to import a smaller set."
                : $"{preview.Issues.Count} open issue(s) found.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task<bool> ImportAsync()
    {
        if (CurrentSlug is null || SelectedColumn is null)
            return false;

        var selected = Issues.Where(issue => issue.IsSelected && issue.CanSelect).Select(issue => issue.Number).ToList();
        if (selected.Count == 0)
        {
            ErrorMessage = "Select at least one issue to import.";
            return false;
        }

        IsBusy = true;
        ErrorMessage = "";
        try
        {
            var result = await importService.ImportAsync(CurrentSlug, new GitHubIssueImportRequest(
                Repository,
                NormalizeLabel(Label),
                SelectedColumn.Id,
                selected));

            if (result is Err<GitHubIssueImportResult> err)
            {
                ErrorMessage = err.Error.Message;
                return false;
            }

            var importResult = ((Ok<GitHubIssueImportResult>)result).Value.BoardResult;
            ItemsImported = importResult.Imported;
            ImportResults.Clear();
            foreach (var item in importResult.Items)
            {
                var reason = string.IsNullOrWhiteSpace(item.Reason) ? "" : $" - {item.Reason}";
                ImportResults.Add($"{item.Status}: {item.Title} ({item.ExternalId}){reason}");
            }
            ResultMessage = $"Imported {importResult.Imported}; skipped {importResult.SkippedExisting}; failed {importResult.Failed}.";
            return importResult.Failed == 0 && importResult.SkippedExisting == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshColumns()
    {
        Columns.Clear();
        if (CurrentSlug is null)
            return;

        var board = boardService.LoadBoard(CurrentSlug);
        foreach (var column in board.Columns)
            Columns.Add(new ImportColumnOption(column.Title, column.Id.Value));

        SelectedColumn = Columns.FirstOrDefault(c => c.Id == ColumnId.Backlog.Value) ?? Columns.FirstOrDefault();
    }

    private static string? NormalizeLabel(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 2: Register the view model**

Modify `ai-dev.ui.winui/App.xaml.cs`:

```csharp
services.AddTransient<GitHubIssueImportViewModel>();
```

- [ ] **Step 3: Build WinUI project**

Run:

```bash
dotnet build ai-dev.ui.winui/ai-dev.ui.winui.csproj -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add ai-dev.ui.winui/ViewModels/GitHubIssueImportViewModel.cs ai-dev.ui.winui/App.xaml.cs
git commit -m "feat: add github import dialog state"
```

### Task 7: WinUI Dialog And Board Entry Point

**Files:**
- Create: `ai-dev.ui.winui/Views/Dialogs/GitHubIssueImportDialog.xaml.cs`
- Modify: `ai-dev.ui.winui/Views/Pages/BoardPage.xaml`
- Modify: `ai-dev.ui.winui/Views/Pages/BoardPage.xaml.cs`
- Modify: `ai-dev.ui.winui/Views/Dialogs/TaskDialog.xaml.cs`

- [ ] **Step 1: Create import dialog**

Create `ai-dev.ui.winui/Views/Dialogs/GitHubIssueImportDialog.xaml.cs`:

```csharp
using AiDev.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace AiDev.WinUI.Views.Dialogs;

public sealed class GitHubIssueImportDialog : ContentDialog
{
    private readonly GitHubIssueImportViewModel _viewModel;
    private readonly TextBlock _message;

    public bool ImportedAny { get; private set; }

    public GitHubIssueImportDialog(GitHubIssueImportViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.Initialize();

        Title = "Import from GitHub";
        PrimaryButtonText = "Import selected";
        SecondaryButtonText = "Preview";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Secondary;

        var repositoryBox = new TextBox { PlaceholderText = "owner/repo", Text = _viewModel.Repository };
        repositoryBox.TextChanged += (_, _) => _viewModel.Repository = repositoryBox.Text;

        var labelBox = new TextBox { PlaceholderText = "Optional exact label" };
        labelBox.TextChanged += (_, _) => _viewModel.Label = labelBox.Text;

        var columnCombo = new ComboBox
        {
            ItemsSource = _viewModel.Columns,
            DisplayMemberPath = nameof(ImportColumnOption.Title),
            SelectedItem = _viewModel.SelectedColumn,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        columnCombo.SelectionChanged += (_, _) =>
        {
            if (columnCombo.SelectedItem is ImportColumnOption option)
                _viewModel.SelectedColumn = option;
        };

        var issuesList = new ListView
        {
            ItemsSource = _viewModel.Issues,
            Height = 320,
            SelectionMode = ListViewSelectionMode.None
        };
        issuesList.ItemTemplate = BuildIssueTemplate();

        var resultList = new ListView
        {
            ItemsSource = _viewModel.ImportResults,
            Height = 120,
            SelectionMode = ListViewSelectionMode.None
        };

        _message = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
        };

        var panel = new StackPanel { Width = 720, Spacing = 12 };
        panel.Children.Add(BuildField("Repository", repositoryBox));
        panel.Children.Add(BuildField("Label filter", labelBox));
        panel.Children.Add(BuildField("Target column", columnCombo));
        panel.Children.Add(issuesList);
        panel.Children.Add(_message);
        panel.Children.Add(resultList);
        Content = panel;

        SecondaryButtonClick += OnPreviewClick;
        PrimaryButtonClick += OnImportClick;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GitHubIssueImportViewModel.ErrorMessage) or nameof(GitHubIssueImportViewModel.ResultMessage))
                RefreshMessage();
        };
    }

    private static DataTemplate BuildIssueTemplate()
    {
        return (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid ColumnSpacing="8" Padding="0,6">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <CheckBox Grid.Column="0" IsChecked="{Binding IsSelected, Mode=TwoWay}" IsEnabled="{Binding CanSelect}" />
                    <TextBlock Grid.Column="1" Text="{Binding IssueNumberText}" VerticalAlignment="Top" Style="{StaticResource CaptionTextBlockStyle}" />
                    <StackPanel Grid.Column="2" Spacing="2">
                        <TextBlock Text="{Binding Title}" TextWrapping="Wrap" />
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <TextBlock Text="{Binding CreatedAtText}" Style="{StaticResource CaptionTextBlockStyle}" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                            <TextBlock Text="{Binding LabelsText}" Style="{StaticResource CaptionTextBlockStyle}" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                        </StackPanel>
                    </StackPanel>
                    <TextBlock Grid.Column="3" Text="{Binding StatusText}" Style="{StaticResource CaptionTextBlockStyle}" />
                </Grid>
            </DataTemplate>
            """);
    }

    private static StackPanel BuildField(string label, Control input)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        panel.Children.Add(input);
        return panel;
    }

    private async void OnPreviewClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var deferral = args.GetDeferral();
        try
        {
            await _viewModel.PreviewAsync();
            RefreshMessage();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnImportClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var deferral = args.GetDeferral();
        try
        {
            var clean = await _viewModel.ImportAsync();
            ImportedAny = _viewModel.ItemsImported > 0;
            RefreshMessage();
            if (clean)
                Hide();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void RefreshMessage()
    {
        _message.Text = string.IsNullOrWhiteSpace(_viewModel.ErrorMessage)
            ? _viewModel.ResultMessage
            : _viewModel.ErrorMessage;
    }
}
```

- [ ] **Step 2: Add Board page button**

Modify the header button area in `ai-dev.ui.winui/Views/Pages/BoardPage.xaml`:

```xml
<StackPanel HorizontalAlignment="Right" VerticalAlignment="Bottom" Orientation="Horizontal" Spacing="8">
    <Button Click="ImportGitHub_Click"
            Content="Import from GitHub"
            Padding="10,6" />
    <Button Command="{Binding RefreshCommand}"
            Content="&#x21BB; Refresh"
            Padding="10,6" />
</StackPanel>
```

- [ ] **Step 3: Open dialog from Board page**

Add to `ai-dev.ui.winui/Views/Pages/BoardPage.xaml.cs`:

```csharp
private async void ImportGitHub_Click(object sender, RoutedEventArgs e)
{
    var vm = App.Services.GetRequiredService<GitHubIssueImportViewModel>();
    var dialog = new GitHubIssueImportDialog(vm) { XamlRoot = XamlRoot };
    await dialog.ShowAsync();
    if (dialog.ImportedAny)
        ViewModel.Refresh();
}
```

- [ ] **Step 4: Show source metadata in task dialog**

In `ai-dev.ui.winui/Views/Dialogs/TaskDialog.xaml.cs`, add this import:

```csharp
using AiDev.Features.Board;
```

When `viewModel.IsEditing` and the edited task has source metadata available, add a read-only row. If `BoardViewModel` does not expose the edited task, add this property:

```csharp
[ObservableProperty] public partial ExternalTaskSource? EditingExternalSource { get; set; }
```

Set it in `OpenNewTask`:

```csharp
EditingExternalSource = null;
```

Set it in `OpenEditTask`:

```csharp
EditingExternalSource = task.ExternalSource;
```

Then add to `TaskDialog` panel before the description field:

```csharp
if (viewModel.EditingExternalSource is { } source)
{
    var sourcePanel = new StackPanel { Spacing = 4 };
    var sourceText = new TextBlock
    {
        Text = $"{source.Provider}: {source.Id}",
        TextWrapping = TextWrapping.Wrap,
        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
    };
    sourcePanel.Children.Add(BuildLabel("Source"));
    sourcePanel.Children.Add(sourceText);
    panel.Children.Add(sourcePanel);
}
```

- [ ] **Step 5: Build WinUI project**

Run:

```bash
dotnet build ai-dev.ui.winui/ai-dev.ui.winui.csproj -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add ai-dev.ui.winui/Views/Dialogs/GitHubIssueImportDialog.xaml.cs ai-dev.ui.winui/Views/Pages/BoardPage.xaml ai-dev.ui.winui/Views/Pages/BoardPage.xaml.cs ai-dev.ui.winui/Views/Dialogs/TaskDialog.xaml.cs ai-dev.ui.winui/ViewModels/BoardViewModel.cs
git commit -m "feat: add winui github issue import dialog"
```

### Task 8: Full Verification

**Files:**
- Verify all changed files.

- [ ] **Step 1: Run unit tests**

Run:

```bash
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj
```

Expected: all unit tests pass.

- [ ] **Step 2: Run solution build**

Run:

```bash
dotnet build ai-dev-net.slnx
```

Expected: build succeeds with zero warnings because `TreatWarningsAsErrors=true`.

- [ ] **Step 3: Manual WinUI smoke test**

Run:

```bash
dotnet run --project ai-dev.ui.winui -p:Platform=x64
```

Expected:
- Board page opens.
- `Import from GitHub` button opens a dialog.
- Detected repo is filled when the active codebase has a GitHub remote.
- Missing token shows the configured missing-token message.
- With a valid token, preview loads open issues.
- Already-imported issues are disabled.
- Import creates local board tasks in the selected column.
- Repeating import skips the same issues.

- [ ] **Step 4: Commit final polish**

```bash
git status --short
git add ai-dev.core ai-dev.ui.winui ai-dev-net.tests.unit
git commit -m "test: verify github issue import"
```

---

## Self-Review

### Spec Coverage

- One-way WinUI import: covered by Tasks 5, 6, and 7.
- Shared core service: covered by Tasks 4 and 5.
- External metadata and dedupe: covered by Tasks 1, 2, and 5.
- Existing GitHub token: covered by Task 4.
- Open issues only, optional label filter, 500 cap, pull request filtering: covered by Task 4.
- No tags imported: covered by Task 5 tests and mapping.
- Full body plus URL: covered by Task 5 tests and mapping.
- Already-imported preview rows disabled: covered by Task 5 and Task 7.
- Read-only task source display: covered by Task 7.
- No GitHub writes, no comments, no Project v2, no API route: preserved by file structure and client contract.

### Placeholder Scan

This plan contains no deferred implementation marker and no step that asks for generic edge-case handling without concrete code.

### Type Consistency

The plan consistently uses:
- `ExternalTaskSource`
- `BoardTaskImportCandidate`
- `BoardTaskImportResult`
- `GitHubRepository`
- `GitHubIssueSummary`
- `GitHubIssueImportService`
- `GitHubIssueImportViewModel`

---

## Execution Options

Plan complete and saved to `docs/superpowers/plans/2026-05-31-github-issues-import-winui.md`. Two execution options:

**1. Subagent-Driven (recommended)** - Dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.
