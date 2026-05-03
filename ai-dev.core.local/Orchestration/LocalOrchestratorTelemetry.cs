using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AiDev.Core.Local.Orchestration;

internal static class LocalOrchestratorTelemetry
{
    public const string ActivitySourceName = "AiDev.LocalOrchestrator";
    public const string MeterName = "AiDev.LocalOrchestrator";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<int> LoopIterations = Meter.CreateCounter<int>(
        "aidev.loop.iterations",
        description: "Number of orchestration loop iterations executed.");

    public static readonly Counter<int> LoopToolCalls = Meter.CreateCounter<int>(
        "aidev.loop.tool_calls",
        description: "Number of tool calls dispatched by the orchestrator.");

    public static readonly Histogram<int> CompactionTokensSaved = Meter.CreateHistogram<int>(
        "aidev.compaction.tokens_saved",
        unit: "tokens",
        description: "Estimated tokens eliminated by a compaction pass.");

    public static readonly Counter<int> LoopSuccesses = Meter.CreateCounter<int>(
        "aidev.loop.successes",
        description: "Number of objectives that completed successfully.");

    public static readonly Counter<int> LoopFailures = Meter.CreateCounter<int>(
        "aidev.loop.failures",
        description: "Number of objectives that terminated with an error.");
}
