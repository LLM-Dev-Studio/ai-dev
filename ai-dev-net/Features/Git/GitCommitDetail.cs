namespace AiDevNet.Features.Git;

public class GitCommitDetail
{
    public GitCommit Commit { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    public string Diff { get; set; } = string.Empty;
}
