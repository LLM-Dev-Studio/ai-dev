using AiDev.Features.Decision;

using Microsoft.Extensions.Logging.Abstractions;

using UnitResult = AiDev.Models.Unit;

namespace AiDevNet.Tests.Unit;

public class DecisionsServiceTests
{
    private static DecisionsService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        dispatcher.Dispatch(Arg.Any<IReadOnlyList<DomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new Ok<UnitResult>(UnitResult.Value));
        return new DecisionsService(paths, dispatcher, new AtomicFileWriter(), new ProjectMutationCoordinator(), NullLogger<DecisionsService>.Instance);
    }

    private static (ProjectSlug Project, DecisionId Id) SeedPendingDecision(WorkspacePaths paths, string body = "Which approach should we take?")
    {
        var projectSlug = new ProjectSlug("test-project");
        var pendingDir = paths.DecisionsPendingDir(projectSlug);
        Directory.CreateDirectory(pendingDir);
        const string filename = "20260510-120000-test-decision.md";
        var fields = new Dictionary<string, string>
        {
            ["from"] = "pm-agent",
            ["date"] = DateTime.UtcNow.ToString("o"),
            ["priority"] = "normal",
            ["subject"] = "Test Decision",
            ["status"] = "pending",
        };
        File.WriteAllText(Path.Combine(pendingDir, filename), FrontmatterParser.Stringify(fields, body));
        return (projectSlug, new DecisionId("20260510-120000-test-decision"));
    }

    [Fact]
    public void CreateDecision_WritesPendingFile()
    {
        var svc = CreateService(out var paths);
        var project = new ProjectSlug("test-project");

        var result = svc.CreateDecision(project, "pm-agent", "Pick a DB", Priority.Normal, null, "Should we use Postgres or SQLite?");

        result.ShouldBeOfType<Ok<UnitResult>>();
        var files = Directory.GetFiles(paths.DecisionsPendingDir(project), "*.md");
        files.Length.ShouldBe(1);
    }

    [Fact]
    public void ListDecisions_ReturnsPendingByDefault()
    {
        var svc = CreateService(out var paths);
        var (project, _) = SeedPendingDecision(paths);

        var decisions = svc.ListDecisions(project);

        decisions.Count.ShouldBe(1);
        decisions[0].Status.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void ListDecisions_WhenResolvedRequested_ReturnsOnlyResolved()
    {
        var svc = CreateService(out var paths);
        var (project, _) = SeedPendingDecision(paths);

        var decisions = svc.ListDecisions(project, DecisionStatus.Resolved);

        decisions.ShouldBeEmpty();
    }

    [Fact]
    public void GetDecision_FindsPendingDecision()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);

        var decision = svc.GetDecision(project, id);

        decision.ShouldNotBeNull();
        decision.Id.ShouldBe(id);
        decision.Status.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void GetDecision_ReturnsNullForNonExistentId()
    {
        var svc = CreateService(out var paths);
        var project = new ProjectSlug("test-project");

        var decision = svc.GetDecision(project, new DecisionId("does-not-exist"));

        decision.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveDecisionAsync_MovesFileFromPendingToResolved()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);

        var result = await svc.ResolveDecisionAsync(project, id, "Go with Postgres.", TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<UnitResult>>();
        File.Exists(Path.Combine(paths.DecisionsPendingDir(project), $"{id.Value}.md")).ShouldBeFalse();
        File.Exists(Path.Combine(paths.DecisionsResolvedDir(project), $"{id.Value}.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveDecisionAsync_WritesResponseToResolvedFile()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);
        const string response = "Go with Postgres.";

        await svc.ResolveDecisionAsync(project, id, response, TestContext.Current.CancellationToken);

        var content = File.ReadAllText(Path.Combine(paths.DecisionsResolvedDir(project), $"{id.Value}.md"));
        content.ShouldContain(response);
        content.ShouldContain("## Human Response");
    }

    [Fact]
    public async Task ResolveDecisionAsync_DeletesPendingFile()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);

        await svc.ResolveDecisionAsync(project, id, "Approved.", TestContext.Current.CancellationToken);

        var pendingPath = Path.Combine(paths.DecisionsPendingDir(project), $"{id.Value}.md");
        File.Exists(pendingPath).ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveDecisionAsync_UpdatesStatusToResolved()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);

        await svc.ResolveDecisionAsync(project, id, "Approved.", TestContext.Current.CancellationToken);

        var resolved = svc.GetDecision(project, id);
        resolved.ShouldNotBeNull();
        resolved.Status.IsResolved.ShouldBeTrue();
        resolved.Response.ShouldBe("Approved.");
        resolved.ResolvedBy.ShouldBe("human");
    }

    [Fact]
    public async Task ResolveDecisionAsync_RoundTrip_ParsesResponseCorrectly()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths, "Which option do we choose?");
        const string response = "We go with option B because it reduces risk.";

        await svc.ResolveDecisionAsync(project, id, response, TestContext.Current.CancellationToken);
        var resolved = svc.GetDecision(project, id);

        resolved.ShouldNotBeNull();
        resolved.Response.ShouldBe(response);
        resolved.Body.ShouldBe("Which option do we choose?");
    }

    [Fact]
    public async Task ResolveDecisionAsync_WhenDecisionNotFound_ReturnsError()
    {
        var svc = CreateService(out var paths);
        var project = new ProjectSlug("test-project");

        var result = await svc.ResolveDecisionAsync(project, new DecisionId("does-not-exist"), "Some response.", TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<UnitResult>>();
        err.Error.Code.ShouldBe("DECISION_NOT_FOUND");
    }

    [Fact]
    public async Task ResolveDecisionAsync_WhenAlreadyResolved_ReturnsError()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);
        await svc.ResolveDecisionAsync(project, id, "First answer.", TestContext.Current.CancellationToken);

        var result = await svc.ResolveDecisionAsync(project, id, "Second answer.", TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<UnitResult>>();
        err.Error.Code.ShouldBe("DECISION_ALREADY_RESOLVED");
    }

    [Fact]
    public async Task ResolveDecisionAsync_WhenResponseIsWhitespace_ReturnsError()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);

        var result = await svc.ResolveDecisionAsync(project, id, "   ", TestContext.Current.CancellationToken);

        var err = result.ShouldBeOfType<Err<UnitResult>>();
        err.Error.Code.ShouldBe("DECISION_INVALID_RESPONSE");
    }

    [Fact]
    public async Task ListDecisions_AfterResolve_ReturnsResolvedDecision()
    {
        var svc = CreateService(out var paths);
        var (project, id) = SeedPendingDecision(paths);
        await svc.ResolveDecisionAsync(project, id, "Done.", TestContext.Current.CancellationToken);

        var resolved = svc.ListDecisions(project, DecisionStatus.Resolved);
        var pending = svc.ListDecisions(project, DecisionStatus.Pending);

        resolved.Count.ShouldBe(1);
        resolved[0].Id.ShouldBe(id);
        pending.ShouldBeEmpty();
    }
}
