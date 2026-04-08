namespace AiDev.Features.Git;

public class GitCommit
{
    public required string Hash { get; set; }
    public required string ShortHash { get; set; }
    public required string Subject { get; set; }
    public required string Author { get; set; }
    public required string AuthorEmail { get; set; }
    public required string Date { get; set; }
}
