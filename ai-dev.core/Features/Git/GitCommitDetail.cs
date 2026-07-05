namespace AiDev.Features.Git;

/// <summary>
/// Represents detailed information for a Git commit.
/// </summary>
public class GitCommitDetail
{
    /// <summary>
    /// Gets or sets the commit summary.
    /// </summary>
    public required GitCommit Commit { get; set; }

    /// <summary>
    /// Gets or sets the commit body message.
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the diff output for the commit.
    /// </summary>
    public required string Diff { get; set; }
}
