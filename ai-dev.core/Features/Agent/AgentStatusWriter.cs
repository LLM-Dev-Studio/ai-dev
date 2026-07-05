namespace AiDev.Features.Agent;

/// <summary>
/// Writes partial status updates into an agent metadata file.
/// </summary>
public partial class AgentStatusWriter(ILogger<AgentStatusWriter> logger)
{
    /// <summary>
    /// Applies status field updates to an agent metadata file.
    /// </summary>
    /// <param name="agentDir">The agent directory containing <c>agent.json</c>.</param>
    /// <param name="updates">The status fields to merge into the existing metadata.</param>
    /// <returns>A task that completes when the update finishes.</returns>
    public async Task UpdateAsync(string agentDir, Dictionary<string, object?> updates)
    {
        var path = Path.Combine(agentDir, "agent.json");
        try
        {
            // If the file is corrupt, skip the update entirely — better to leave it unchanged
            // than to overwrite it with only status fields, which would destroy slug/model/executor config.
            Dictionary<string, JsonElement> existing = [];
            if (File.Exists(path))
            {
                existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    await File.ReadAllTextAsync(path), JsonDefaults.Read) ?? [];
            }

            var merged = existing.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            foreach (var (k, v) in updates)
            {
                if (v == null) merged.Remove(k);
                // Serialize each value to JsonElement to guarantee proper JSON escaping
                // (avoids issues with special characters in strings going through object? boxing).
                else merged[k] = JsonSerializer.SerializeToElement(v);
            }

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(merged, JsonDefaults.Write));
        }
        catch (Exception ex) { LogStatusUpdateFailed(ex, agentDir); }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[runner] Failed to update agent status in {AgentDir}")]
    private partial void LogStatusUpdateFailed(Exception ex, string agentDir);
}
