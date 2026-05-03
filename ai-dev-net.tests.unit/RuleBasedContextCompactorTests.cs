using AiDev.Core.Local.Contracts;
using AiDev.Core.Local.Implementation;

namespace AiDevNet.Tests.Unit;

public class RuleBasedContextCompactorTests
{
    private static readonly RuntimeModelProfile AnyProfile = new(
        "test-model", "large-local", "ollama", 32_000, false);

    private static LocalRuntimeState StateWith(
        IReadOnlyList<RuntimeObservation>? observations = null,
        IReadOnlyList<RuntimeDecision>? decisions = null)
        => new(
            Objective: new LocalObjective("Find all services", null, null, Guid.NewGuid()),
            Transcript: new RuntimeTranscript(
                observations ?? [],
                decisions ?? []),
            Budget: new RuntimeBudget(50, 10, 3, 32_000),
            ModelProfile: AnyProfile,
            StartedAtUtc: DateTimeOffset.UtcNow,
            Iteration: 1);

    private static RuntimeObservation Obs(string source, string summary, string[]? evidence = null)
        => new(DateTimeOffset.UtcNow, source, summary, evidence ?? []);

    private static RuntimeDecision Decision(string text, DateTimeOffset? at = null)
        => new(at ?? DateTimeOffset.UtcNow, text, "rationale");

    [Fact]
    public void Compact_WithEmptyTranscript_ReturnsEmptySnapshot()
    {
        var state = StateWith();
        var result = new RuleBasedContextCompactor().Compact(state);
        var snapshot = result.ShouldBeOfType<Ok<CompactionSnapshot>>().Value;
        snapshot.Facts.ShouldBeEmpty();
        snapshot.OpenQuestions.ShouldBeEmpty();
        snapshot.EstimatedTokens.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Compact_DuplicateObservations_KeepsOnlyLastOccurrence()
    {
        var early = Obs("tool:read", "File has 10 lines") with { AtUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var late = Obs("tool:read", "File has 10 lines") with { AtUtc = DateTimeOffset.UtcNow };

        var state = StateWith(observations: [early, late]);
        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        snapshot.Facts.Count.ShouldBe(1);
        snapshot.Facts[0].Fact.ShouldBe("File has 10 lines");
    }

    [Fact]
    public void Compact_ObservationWithTwoPlusCitations_MarkedStable()
    {
        var obs = Obs("tool:grep", "Found 3 usages", ["file.cs:10", "file.cs:20"]);
        var state = StateWith(observations: [obs]);

        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        snapshot.Facts.Single().IsStable.ShouldBeTrue();
    }

    [Fact]
    public void Compact_ObservationWithOneCitation_NotStable()
    {
        var obs = Obs("tool:read", "Interface found", ["file.cs:5"]);
        var state = StateWith(observations: [obs]);

        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        snapshot.Facts.Single().IsStable.ShouldBeFalse();
    }

    [Fact]
    public void Compact_EvidenceFreeObservations_DroppedExceptLastTwo()
    {
        var old1 = Obs("tool:run", "Step 1 output") with { AtUtc = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var old2 = Obs("tool:run", "Step 2 output") with { AtUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var recent1 = Obs("tool:run", "Step 3 output") with { AtUtc = DateTimeOffset.UtcNow.AddMinutes(-2) };
        var recent2 = Obs("tool:run", "Step 4 output") with { AtUtc = DateTimeOffset.UtcNow };

        var state = StateWith(observations: [old1, old2, recent1, recent2]);
        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        // old1 and old2 dropped; recent1 and recent2 kept as tail
        snapshot.Facts.Count.ShouldBe(2);
        snapshot.Facts.Select(f => f.Fact).ShouldContain("Step 3 output");
        snapshot.Facts.Select(f => f.Fact).ShouldContain("Step 4 output");
    }

    [Fact]
    public void Compact_ObservationWithEvidence_KeptEvenIfOld()
    {
        var withEvidence = Obs("tool:grep", "Critical interface", ["core.cs:42"]) with
        {
            AtUtc = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
        var noEvidenceTail1 = Obs("tool:run", "Tail 1") with { AtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var noEvidenceTail2 = Obs("tool:run", "Tail 2") with { AtUtc = DateTimeOffset.UtcNow };

        var state = StateWith(observations: [withEvidence, noEvidenceTail1, noEvidenceTail2]);
        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        snapshot.Facts.Select(f => f.Fact).ShouldContain("Critical interface");
    }

    [Fact]
    public void Compact_WithDecisions_ExtractsOpenQuestions()
    {
        var decisions = new[]
        {
            Decision("Should we use FileSystem or InMemory store?"),
            Decision("Which executor handles Ollama?"),
        };
        var state = StateWith(decisions: decisions);

        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        snapshot.OpenQuestions.Count.ShouldBe(2);
    }

    [Fact]
    public void Compact_SupersededDecisions_KeepsLatestPerSubject()
    {
        var old = Decision("Should we use FileSystem", DateTimeOffset.UtcNow.AddMinutes(-5));
        var latest = Decision("Should we use FileSystem or InMemory?", DateTimeOffset.UtcNow);

        var state = StateWith(decisions: [old, latest]);
        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        // Both start with "Should we use FileSystem" (50-char subject key) — grouped to 1
        snapshot.OpenQuestions.Count.ShouldBe(1);
        snapshot.OpenQuestions[0].ShouldBe("Should we use FileSystem or InMemory?");
    }

    [Fact]
    public void Compact_EstimatedTokensReflectContentSize()
    {
        var obs = Obs("tool:read", new string('x', 400), ["cite1", "cite2"]);
        var state = StateWith(observations: [obs]);

        var snapshot = new RuleBasedContextCompactor().Compact(state).ShouldBeOfType<Ok<CompactionSnapshot>>().Value;

        snapshot.EstimatedTokens.ShouldBeGreaterThan(90);
    }
}
