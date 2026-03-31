namespace AiDev.Services;

public enum OllamaHealthStatus { Unknown, Connected, Unreachable }

/// <summary>
/// Background service that periodically checks whether the configured Ollama instance
/// is reachable by calling GET /api/tags. Exposes the current status and fires
/// <see cref="Changed"/> whenever the status transitions.
/// </summary>
public class OllamaHealthService(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService,
    ILogger<OllamaHealthService> logger) : BackgroundService
{
    public OllamaHealthStatus Status { get; private set; } = OllamaHealthStatus.Unknown;
    public DateTimeOffset? LastChecked { get; private set; }

    public event Action? Changed;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Check immediately on startup, then every 30 seconds.
        while (!ct.IsCancellationRequested)
        {
            await CheckAsync(ct).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var baseUrl = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/tags";

        OllamaHealthStatus newStatus;
        try
        {
            // Use a short-lived client with a tight timeout — this is a health probe, not inference.
            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            newStatus = response.IsSuccessStatusCode ? OllamaHealthStatus.Connected : OllamaHealthStatus.Unreachable;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "[ollama-health] Probe failed: {Message}", ex.Message);
            newStatus = OllamaHealthStatus.Unreachable;
        }

        LastChecked = DateTimeOffset.UtcNow;
        var previous = Status;
        Status = newStatus;

        if (newStatus != previous)
            Changed?.Invoke();
    }
}
