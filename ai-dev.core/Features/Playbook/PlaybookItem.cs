namespace AiDev.Features.Playbook;

/// <summary>
/// Represents a playbook summary.
/// </summary>
public class PlaybookItem
{
    /// <summary>
    /// Gets or sets the playbook slug.
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Gets or sets the playbook title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional macro shorthand from frontmatter (e.g. <c>!deploy-check</c>).
    /// </summary>
    public string? Macro { get; set; }
}
