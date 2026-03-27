namespace AiDevNet.Services;

public class AgentTemplate
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "sonnet";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
