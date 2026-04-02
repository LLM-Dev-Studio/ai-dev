namespace AiDev.Features.KnowledgeBase;

public class KbArticle
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// Optional trigger phrase from frontmatter. When set, this article is only injected
    /// into the agent prompt if a trigger word appears in the inbox message body.
    /// When null, the article is always injected.
    /// </summary>
    public string? Trigger { get; set; }
}
