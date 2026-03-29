namespace AiDevNet.Models;

public class MessageItem
{
    public string Filename { get; set; } = string.Empty;
    public AgentSlug AgentSlug { get; set; } = null!;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string Priority { get; set; } = "normal";
    public string Re { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public TaskId? TaskId { get; set; }
}
