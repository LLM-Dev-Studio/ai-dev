using AiDev.Core.Local.Contracts;
using System.Text;

namespace AiDev.Core.Local.Implementation;

internal sealed class RuleBasedContextCompactor : IContextCompactor
{
    // Always retain the last two observations so recent success/failure is never lost.
    private const int AlwaysKeepTail = 2;

    public Result<CompactionSnapshot> Compact(LocalRuntimeState state)
    {
        var kept = ApplyKeepDropRules(state.Transcript.Observations);
        var facts = BuildFacts(kept);
        var openQuestions = BuildOpenQuestions(state.Transcript.Decisions);
        var summary = BuildSummary(state.Objective, facts, state.Transcript.Decisions);
        var estimatedTokens = EstimateTokens(summary, facts, openQuestions);

        return new Ok<CompactionSnapshot>(new CompactionSnapshot(summary, facts, openQuestions, estimatedTokens));
    }

    // Keep:  observations with direct evidence (stable facts, unresolved errors)
    //        the last AlwaysKeepTail entries (captures recent success/failure)
    // Drop:  repeated observations (same source + summary)
    //        evidence-free observations that are not in the tail
    private static IReadOnlyList<RuntimeObservation> ApplyKeepDropRules(
        IReadOnlyList<RuntimeObservation> observations)
    {
        if (observations.Count == 0) return [];

        // Remove repeated observations — keep the most recent occurrence.
        var seen = new HashSet<(string Source, string Summary)>();
        var unique = observations
            .OrderBy(o => o.AtUtc)
            .Where(o => seen.Add((o.Source, o.Summary)))
            .ToList();

        var tail = unique.TakeLast(AlwaysKeepTail).ToHashSet(ReferenceEqualityComparer.Instance);

        return unique
            .Where(o => tail.Contains(o) || o.Evidence.Count > 0)
            .ToList();
    }

    // A fact is stable when it carries at least two independent citations.
    private static IReadOnlyList<RuntimeFact> BuildFacts(IReadOnlyList<RuntimeObservation> observations)
        => observations
            .Select(o => new RuntimeFact(
                Category: o.Source,
                Fact: o.Summary,
                Citations: o.Evidence,
                IsStable: o.Evidence.Count >= 2))
            .ToList();

    // Open questions = latest decision per subject (superseded decisions dropped).
    private static IReadOnlyList<string> BuildOpenQuestions(IReadOnlyList<RuntimeDecision> decisions)
    {
        if (decisions.Count == 0) return [];

        return decisions
            .GroupBy(d => SubjectKey(d.Decision))
            .Select(g => g.OrderByDescending(d => d.AtUtc).First())
            .Select(d => d.Decision)
            .ToList();
    }

    // Use the first four words as the subject key so revisions to the same question group together.
    private static string SubjectKey(string decision)
    {
        var words = decision.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Take(4));
    }

    private static string BuildSummary(
        LocalObjective objective,
        IReadOnlyList<RuntimeFact> facts,
        IReadOnlyList<RuntimeDecision> decisions)
    {
        if (facts.Count == 0 && decisions.Count == 0)
            return $"Objective: {objective.Goal}. No observations recorded.";

        var sb = new StringBuilder();
        sb.Append($"Objective: {objective.Goal}.");
        if (facts.Count > 0) sb.Append($" {facts.Count} fact(s) retained.");
        if (decisions.Count > 0) sb.Append($" {decisions.Count} decision(s) in transcript.");
        return sb.ToString();
    }

    // Rough approximation: 4 characters ≈ 1 token.
    private static int EstimateTokens(
        string summary,
        IReadOnlyList<RuntimeFact> facts,
        IReadOnlyList<string> openQuestions)
    {
        var chars = summary.Length
            + facts.Sum(f => f.Fact.Length + f.Citations.Sum(c => c.Length))
            + openQuestions.Sum(q => q.Length);
        return Math.Max(1, chars / 4);
    }
}
