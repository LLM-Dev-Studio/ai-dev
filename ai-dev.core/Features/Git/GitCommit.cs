namespace AiDev.Features.Git;

public class GitCommit
{
    public string Hash { get; set; } = string.Empty;
    public string ShortHash { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}
