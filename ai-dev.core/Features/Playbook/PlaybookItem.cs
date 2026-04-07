namespace AiDev.Features.Playbook;

public class PlaybookItem
{
    public required string Slug { get; set; }
    public required string Title { get; set; }
    /// <summary>
    /// Optional macro shorthand from frontmatter (e.g. <c>!deploy-check</c>).
    /// </summary>
    public string? Macro { get; set; }
}
