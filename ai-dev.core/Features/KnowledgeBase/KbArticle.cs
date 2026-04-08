namespace AiDev.Features.KnowledgeBase;

public class KbArticle
{
    public required string Slug { get; set; }
    public required string Title { get; set; }
    /// <summary>
    /// Optional trigger phrase from frontmatter. When set, this article is only injected
    /// into the agent prompt if a trigger word appears in the inbox message body.
    /// When null, the article is always injected.
    /// </summary>
    public string? Trigger { get; set; }
}
