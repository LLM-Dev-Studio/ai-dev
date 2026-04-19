using AiDev.Features.Planning;
using AiDev.Models;

using Microsoft.Extensions.Logging.Abstractions;

namespace AiDevNet.Tests.Unit;

public class PlanningSessionServiceTests
{
    // -------------------------------------------------------------------------
    // Session creation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateSession_ReturnsNewSessionWithPhase1Active()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");

        var session = await service.CreateSessionAsync(slug);

        session.CurrentPhase.ShouldBe(SessionPhase.Phase1BusinessDiscovery);
        session.State.ShouldBe(PlanningSessionState.Active);
        session.Id.ShouldNotBeEmpty();
        session.Phase1LockedAt.ShouldBeNull();
    }

    [Fact]
    public async Task CreateSession_PersistsMetadataToFile()
    {
        var service = CreateService(out var paths);
        var slug    = new ProjectSlug("test-project");

        var session = await service.CreateSessionAsync(slug);

        var metadataFile = paths.PlanningSessionMetadataPath(slug, session.Id);
        metadataFile.Exists().ShouldBeTrue();
    }

    [Fact]
    public async Task GetActiveSession_ReturnsLatestNonCompletedSession()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");

        var session = await service.CreateSessionAsync(slug);
        var active  = service.GetActiveSession(slug);

        active.ShouldNotBeNull();
        active!.Id.ShouldBe(session.Id);
    }

    [Fact]
    public void GetActiveSession_WhenNoSessions_ReturnsNull()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("no-sessions-project");

        var result = service.GetActiveSession(slug);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Conversation persistence
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AppendTurn_GetConversation_RoundTripsSuccessfully()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);

        var turn = new ConversationTurn(
            ConversationRole.User,
            "Hello, I want to build a leave management system.",
            DateTimeOffset.UtcNow,
            SessionPhase.Phase1BusinessDiscovery);

        await service.AppendTurnAsync(slug, session.Id, turn);

        var conversation = service.GetConversation(slug, session.Id);

        conversation.Count.ShouldBe(1);
        conversation[0].Role.ShouldBe(ConversationRole.User);
        conversation[0].Content.ShouldBe("Hello, I want to build a leave management system.");
        conversation[0].Phase.ShouldBe(SessionPhase.Phase1BusinessDiscovery);
    }

    [Fact]
    public async Task AppendTurn_MultipleRoles_PreservesOrder()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);

        await service.AppendTurnAsync(slug, session.Id,
            new ConversationTurn(ConversationRole.User, "Turn 1", DateTimeOffset.UtcNow, SessionPhase.Phase1BusinessDiscovery));
        await service.AppendTurnAsync(slug, session.Id,
            new ConversationTurn(ConversationRole.Assistant, "Turn 2", DateTimeOffset.UtcNow, SessionPhase.Phase1BusinessDiscovery));
        await service.AppendTurnAsync(slug, session.Id,
            new ConversationTurn(ConversationRole.User, "Turn 3", DateTimeOffset.UtcNow, SessionPhase.Phase1BusinessDiscovery));

        var conversation = service.GetConversation(slug, session.Id);

        conversation.Count.ShouldBe(3);
        conversation[0].Role.ShouldBe(ConversationRole.User);
        conversation[1].Role.ShouldBe(ConversationRole.Assistant);
        conversation[2].Role.ShouldBe(ConversationRole.User);
    }

    [Fact]
    public async Task GetConversationForPhase_FiltersToRequestedPhase()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);

        await service.AppendTurnAsync(slug, session.Id,
            new ConversationTurn(ConversationRole.User, "Phase1 message", DateTimeOffset.UtcNow, SessionPhase.Phase1BusinessDiscovery));
        await service.AppendTurnAsync(slug, session.Id,
            new ConversationTurn(ConversationRole.User, "Phase3 message", DateTimeOffset.UtcNow, SessionPhase.Phase3PlanningDecomposition));

        var phase1 = service.GetConversationForPhase(slug, session.Id, SessionPhase.Phase1BusinessDiscovery);
        var phase3 = service.GetConversationForPhase(slug, session.Id, SessionPhase.Phase3PlanningDecomposition);

        phase1.Count.ShouldBe(1);
        phase1[0].Content.ShouldBe("Phase1 message");
        phase3.Count.ShouldBe(1);
        phase3[0].Content.ShouldBe("Phase3 message");
    }

    // -------------------------------------------------------------------------
    // DSL management
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveDraftDsl_GetDraftDsl_RoundTripsYamlContent()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);
        const string yaml = "version: \"1.0\"\nproject:\n  name: \"Test\"\n";

        await service.SaveDraftDslAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);

        var draft = service.GetDraftDsl(slug, session.Id, SessionPhase.Phase1BusinessDiscovery);
        draft.ShouldBe(yaml.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task LockPhase_Phase1_UpdatesStateToPhase1Locked()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);
        const string yaml = "version: \"1.0\"\n";

        var result = await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);

        result.ShouldBeOfType<Ok<Unit>>();
        var updated = service.GetSession(slug, session.Id);
        updated!.State.ShouldBe(PlanningSessionState.Phase1Locked);
        updated.Phase1LockedAt.ShouldNotBeNull();
        updated.CurrentPhase.ShouldBe(SessionPhase.Phase2SolutionShaping);
    }

    [Fact]
    public async Task LockPhase_WritesImmutableDslFile()
    {
        var service = CreateService(out var paths);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);
        const string yaml = "version: \"1.0\"\n";

        await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);

        var lockedPath = paths.PlanningSessionLockedDslPath(slug, session.Id, "Business.dsl");
        lockedPath.Exists().ShouldBeTrue();
        File.ReadAllText(lockedPath.Value).ShouldBe(yaml);
    }

    [Fact]
    public async Task LockPhase_SecondAttempt_ReturnsError()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);
        const string yaml = "version: \"1.0\"\n";

        await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);
        var secondAttempt = await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);

        secondAttempt.ShouldBeOfType<Err<Unit>>();
    }

    [Fact]
    public async Task LockPhase_Phase2BeforePhase1Locked_ReturnsError()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);

        var result = await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase2SolutionShaping, "version: \"1.0\"\n");

        result.ShouldBeOfType<Err<Unit>>();
    }

    [Fact]
    public async Task LockPhase_Phase3BeforePhase2Locked_ReturnsError()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);
        const string yaml = "version: \"1.0\"\n";

        // Lock Phase 1 first
        await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);

        // Phase 2 not yet locked — Phase 3 should be rejected
        var result = await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase3PlanningDecomposition, yaml);

        result.ShouldBeOfType<Err<Unit>>();
    }

    [Fact]
    public async Task GetLockedDsl_BeforeLock_ReturnsNull()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);

        var result = service.GetLockedDsl(slug, session.Id, SessionPhase.Phase1BusinessDiscovery);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLockedDsl_AfterLock_ReturnsContent()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);
        const string yaml = "version: \"1.0\"\n";

        await service.LockPhaseAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, yaml);
        var content = service.GetLockedDsl(slug, session.Id, SessionPhase.Phase1BusinessDiscovery);

        content.ShouldBe(yaml);
    }

    // -------------------------------------------------------------------------
    // Token counting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateTokenCount_PersistsAndReadsBack()
    {
        var service = CreateService(out _);
        var slug    = new ProjectSlug("test-project");
        var session = await service.CreateSessionAsync(slug);

        await service.UpdateTokenCountAsync(slug, session.Id, SessionPhase.Phase1BusinessDiscovery, 15_000);

        var updated = service.GetSession(slug, session.Id);
        updated!.Phase1InputTokens.ShouldBe(15_000);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static PlanningSessionService CreateService(out WorkspacePaths paths)
    {
        var root = new RootDir(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        paths = new WorkspacePaths(root);
        return new PlanningSessionService(paths, new AtomicFileWriter(), NullLogger<PlanningSessionService>.Instance);
    }
}
