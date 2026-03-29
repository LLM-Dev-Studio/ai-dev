namespace AiDev.Features.Decision;

public class DecisionItem
{
    public string Filename { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string Priority { get; set; } = "normal";
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Blocks { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Response { get; set; }
}
