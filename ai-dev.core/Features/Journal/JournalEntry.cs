namespace AiDev.Features.Journal;

/// <summary>
/// Represents a journal entry file summary.
/// </summary>
public class JournalEntry
{
    /// <summary>
    /// Gets or sets the journal entry date key.
    /// </summary>
    public required string Date { get; set; }

    /// <summary>
    /// Gets or sets the journal entry filename.
    /// </summary>
    public required string Filename { get; set; }
}
