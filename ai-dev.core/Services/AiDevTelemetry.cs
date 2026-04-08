namespace AiDev.Services;

public static class AiDevTelemetry
{
    public const string ActivitySourceName = "AiDevNet.Core";
    public const string MeterName = "AiDevNet.Core";

    public static readonly System.Diagnostics.ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly System.Diagnostics.Metrics.Meter Meter = new(MeterName);

    public static readonly System.Diagnostics.Metrics.Counter<int> ConsistencyChecksStarted = Meter.CreateCounter<int>(
        "aidev.consistency.checks.started",
        description: "Number of consistency checks started.");

    public static readonly System.Diagnostics.Metrics.Counter<int> ConsistencyFindings = Meter.CreateCounter<int>(
        "aidev.consistency.findings",
        description: "Number of consistency findings discovered.");

    public static readonly System.Diagnostics.Metrics.Counter<int> ConsistencyRepairs = Meter.CreateCounter<int>(
        "aidev.consistency.repairs",
        description: "Number of consistency issues auto-repaired.");

    public static readonly System.Diagnostics.Metrics.Histogram<double> ConsistencyCheckDurationMs = Meter.CreateHistogram<double>(
        "aidev.consistency.check.duration",
        unit: "ms",
        description: "Duration of consistency checks.");

    public static readonly System.Diagnostics.Metrics.Histogram<double> ProjectLockWaitMs = Meter.CreateHistogram<double>(
        "aidev.project.lock.wait.duration",
        unit: "ms",
        description: "Time spent waiting for a project mutation lock.");

    public static readonly System.Diagnostics.Metrics.Counter<int> AtomicWrites = Meter.CreateCounter<int>(
        "aidev.atomic.writes",
        description: "Number of atomic file writes attempted.");

    public static readonly System.Diagnostics.Metrics.Counter<int> AtomicWriteFailures = Meter.CreateCounter<int>(
        "aidev.atomic.write.failures",
        description: "Number of atomic file write failures.");

    public static readonly System.Diagnostics.Metrics.Counter<int> DomainDispatchFailures = Meter.CreateCounter<int>(
        "aidev.domain.dispatch.failures",
        description: "Number of domain event dispatch failures.");
}
