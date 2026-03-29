namespace AiDevNet.Features.Board;

public class BoardTask
{
    public TaskId Id { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string? Description { get; set; }
    public string? Assignee { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Timestamp when the task last moved to its current column. Used by overwatch for stall detection.</summary>
    public DateTime? MovedAt { get; set; }
    /// <summary>Timestamp of the last overwatch nudge. Used to enforce nudge cooldown.</summary>
    public DateTime? NudgedAt { get; set; }
}
