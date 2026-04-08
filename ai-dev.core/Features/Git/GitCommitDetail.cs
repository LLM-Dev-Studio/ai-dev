namespace AiDev.Features.Git;

public class GitCommitDetail
{
    public required GitCommit Commit { get; set; }
    public required string Body { get; set; }
    public required string Diff { get; set; }
}
