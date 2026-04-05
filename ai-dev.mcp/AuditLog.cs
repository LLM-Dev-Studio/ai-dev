using System.Text.Json;

namespace AiDev.Mcp;

/// <summary>
/// Append-only structured audit log for all MCP tool calls.
/// Writes one JSON object per line to <c>{workspaceRoot}/.mcp-audit.jsonl</c>.
/// </summary>
public sealed class AuditLog(string workspaceRoot)
{
    private readonly string _logPath = Path.Combine(workspaceRoot, ".mcp-audit.jsonl");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Fire-and-forget wrapper for use in synchronous MCP tool methods.
    /// </summary>
    public void Record(string tool, Dictionary<string, string?> parameters, string status)
        => _ = RecordAsync(tool, parameters, status);

    public async Task RecordAsync(string tool, Dictionary<string, string?> parameters, string status, CancellationToken cancellationToken = default)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            tool,
            parameters,
            status
        };
        var line = JsonSerializer.Serialize(entry);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logPath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
