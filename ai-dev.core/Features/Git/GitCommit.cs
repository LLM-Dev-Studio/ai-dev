namespace AiDev.Features.Git;

/// <summary>
/// Represents summary information for a Git commit.
/// </summary>
public class GitCommit
{
    /// <summary>
    /// Gets or sets the full commit hash.
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    /// Gets or sets the abbreviated commit hash.
    /// </summary>
    public required string ShortHash { get; set; }

    /// <summary>
    /// Gets or sets the commit subject line.
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// Gets or sets the author name.
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// Gets or sets the author email address.
    /// </summary>
    public required string AuthorEmail { get; set; }

    /// <summary>
    /// Gets or sets the commit date in Git output format.
    /// </summary>
    public required string Date { get; set; }
}
