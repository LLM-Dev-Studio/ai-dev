namespace AiDevNet.Models;

public class StudioSettings
{
    public Dictionary<string, string> Models { get; set; } = new();

    /// <summary>Base URL for the Ollama HTTP API. Defaults to http://localhost:11434.</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
}
