using AiDev.Models;

namespace AiDev.Core.Local.Contracts;

public sealed record LocalObjective(
    string Goal,
    string? SuccessCriteria,
    string? CodebaseRoot,
    Guid CorrelationId);

public sealed record LocalRuntimeState(
    LocalObjective Objective,
    RuntimeTranscript Transcript,
    RuntimeBudget Budget,
    RuntimeModelProfile ModelProfile,
    DateTimeOffset StartedAtUtc,
    int Iteration);

public sealed record RuntimeBudget(
    int MaxToolCalls,
    int MaxExpandedFiles,
    int MaxRetriesPerError,
    int MaxContextTokens);

public sealed record RuntimeModelProfile(
    string ModelId,
    string ModelClass,
    string? Provider,
    int MaxInputTokens,
    bool SupportsParallelTools);

public sealed record RuntimeTranscript(
    IReadOnlyList<RuntimeObservation> Observations,
    IReadOnlyList<RuntimeDecision> Decisions);

public sealed record RuntimeObservation(
    DateTimeOffset AtUtc,
    string Source,
    string Summary,
    IReadOnlyList<string> Evidence);

public sealed record RuntimeDecision(
    DateTimeOffset AtUtc,
    string Decision,
    string Rationale);

public sealed record RuntimeActionPlan(
    string Intent,
    IReadOnlyList<ToolRequest> ToolRequests,
    string ExpectedOutcome,
    bool RequiresUserInput);

public sealed record ToolRequest(
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments,
    string Reason);

public sealed record ToolOutcome(
    string ToolName,
    bool Succeeded,
    string Summary,
    IReadOnlyList<string> Evidence,
    DomainError? Error = null);

public sealed record DiscoveryRequest(
    string Query,
    IReadOnlyList<string> CandidatePaths,
    int MaxSlices,
    bool RestrictToCodeFiles);

public sealed record DiscoveryBatch(
    IReadOnlyList<DiscoverySlice> Slices,
    string Synthesis,
    decimal Confidence,
    string RecommendedNextStep);

public sealed record DiscoverySlice(
    string Path,
    int StartLine,
    int EndLine,
    string Summary,
    IReadOnlyList<string> Evidence);

public sealed record CompactionSnapshot(
    string CompactSummary,
    IReadOnlyList<RuntimeFact> Facts,
    IReadOnlyList<string> OpenQuestions,
    int EstimatedTokens);

public sealed record RuntimeFact(
    string Category,
    string Fact,
    IReadOnlyList<string> Citations,
    bool IsStable);
