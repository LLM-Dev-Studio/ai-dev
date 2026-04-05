namespace AiDev.Features.Playbook;

public class PlaybookItem
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// Optional macro shorthand from frontmatter (e.g. <c>!deploy-check</c>).
    /// </summary>
    public string? Macro { get; set; }
}
