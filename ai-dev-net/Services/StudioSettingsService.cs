namespace AiDevNet.Services;

public class StudioSettings
{
    [JsonPropertyName("models")]
    public Dictionary<string, string> Models { get; set; } = new();

    /// <summary>Base URL for the Ollama HTTP API. Defaults to http://localhost:11434.</summary>
    [JsonPropertyName("ollamaBaseUrl")]
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
}

public class StudioSettingsService(WorkspaceService workspace)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["sonnet"] = "claude-sonnet-4-6",
        ["opus"] = "claude-opus-4-6",
        ["haiku"] = "claude-haiku-4-5-20251001",
    };

    public StudioSettings GetSettings()
    {
        var path = workspace.GetStudioSettingsPath();
        if (!File.Exists(path))
            return new() { Models = new(Defaults) };

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<StudioSettings>(json, JsonOptions);
            if (data?.Models == null)
                return new() { Models = new(Defaults) };

            // Merge file values over defaults
            var merged = new Dictionary<string, string>(Defaults);
            foreach (var (k, v) in data.Models)
                merged[k] = v;
            return new() { Models = merged };
        }
        catch
        {
            return new() { Models = new(Defaults) };
        }
    }

    public void SaveSettings(StudioSettings settings)
    {
        var path = workspace.GetStudioSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
