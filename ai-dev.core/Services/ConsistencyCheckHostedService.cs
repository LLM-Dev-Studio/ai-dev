namespace AiDev.Services;

/// <summary>
/// Runs consistency checks during startup and emits results to logs and telemetry.
/// </summary>
public class ConsistencyCheckHostedService(
    ConsistencyCheckService consistencyCheckService,
    ILogger<ConsistencyCheckHostedService> logger) : IHostedService
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(20);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = AiDevTelemetry.ActivitySource.StartActivity("Consistency.Startup", ActivityKind.Internal);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(StartupTimeout);

        try
        {
            var report = await consistencyCheckService.CheckWorkspaceAsync(timeoutCts.Token).ConfigureAwait(false);
            activity?.SetTag("consistency.projects", report.Projects.Count);
            activity?.SetTag("consistency.warnings", report.WarningCount);
            activity?.SetTag("consistency.errors", report.ErrorCount);
            logger.LogInformation("[consistency] Startup scan complete: {Projects} projects, {Warnings} warnings, {Errors} errors",
                report.Projects.Count, report.WarningCount, report.ErrorCount);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("[consistency] Startup scan timed out after {TimeoutSeconds}s", StartupTimeout.TotalSeconds);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
