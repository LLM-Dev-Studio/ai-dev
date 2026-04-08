namespace AiDev.Executors;

public static class OllamaToolSupport
{
    public const string WorkspaceToolSkill = "mcp-workspace";
    private const string UnsupportedToolsMarker = "does not support tools";

    public static bool AreWorkspaceToolsEnabled(IReadOnlyList<string>? enabledSkills) =>
        enabledSkills?.Contains(WorkspaceToolSkill, StringComparer.Ordinal) == true;

    public static bool IsKnownUnsupportedModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return false;

        return GetDisplayModelId(modelId).StartsWith("gemma3", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnsupportedToolsResponse(string? responseBody) =>
        !string.IsNullOrWhiteSpace(responseBody)
        && responseBody.Contains(UnsupportedToolsMarker, StringComparison.OrdinalIgnoreCase);

    public static bool TryGetUnsupportedToolsMessage(string? modelId, string? responseBody, out string message)
    {
        if (IsKnownUnsupportedModel(modelId) || IsUnsupportedToolsResponse(responseBody))
        {
            message = GetUnsupportedToolsMessage(modelId);
            return true;
        }

        message = string.Empty;
        return false;
    }

    public static string GetUnsupportedToolsMessage(string? modelId)
    {
        var displayModelId = GetDisplayModelId(modelId);
        return $"Ollama model '{displayModelId}' does not support workspace tools. Choose a tool-capable model or disable workspace tools for this agent, then run it again.";
    }

    public static string GetDisplayModelId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return "the selected model";

        var lastSlash = modelId.LastIndexOf('/');
        return lastSlash >= 0 ? modelId[(lastSlash + 1)..] : modelId;
    }
}
