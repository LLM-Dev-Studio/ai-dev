namespace AiDev.Features.KnowledgeBase;

/// <summary>
/// Represents a knowledge base article summary.
/// </summary>
public class KbArticle
{
    /// <summary>
    /// Gets or sets the article slug.
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Gets or sets the article title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional trigger phrase from frontmatter. When set, this article is only injected
    /// into the agent prompt if a trigger word appears in the inbox message body.
    /// When null, the article is always injected.
    /// </summary>
    public string? Trigger { get; set; }
}
