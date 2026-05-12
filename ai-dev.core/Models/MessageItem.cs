namespace AiDev.Models;

/// <summary>
/// Represents a message stored in an agent inbox or processed archive.
/// </summary>
public sealed class MessageItem
{
    /// <summary>
    /// Creates a message with validated required fields and normalized optional metadata.
    /// </summary>
    public MessageItem(
        string filename,
        AgentSlug agentSlug,
        MessageSource from,
        string to,
        string re,
        MessageType type,
        string body,
        DateTime? date = null,
        Priority? priority = null,
        bool isProcessed = false,
        TaskId? taskId = null,
        string? playbook = null)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename is required.", nameof(filename));
        ArgumentNullException.ThrowIfNull(agentSlug);
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("To is required.", nameof(to));
        if (string.IsNullOrWhiteSpace(re))
            throw new ArgumentException("Message subject is required.", nameof(re));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body is required.", nameof(body));

        Filename = filename;
        AgentSlug = agentSlug;
        From = from;
        To = to;
        Date = date;
        Priority = NormalizePriority(priority);
        Re = re;
        Type = type;
        Body = body.Trim();
        IsProcessed = isProcessed;
        TaskId = taskId;
        Playbook = NormalizeOptional(playbook);
    }

    /// <summary>
    /// Gets the backing filename for the message.
    /// </summary>
    public string Filename { get; }

    /// <summary>
    /// Gets the agent slug that owns the message.
    /// </summary>
    public AgentSlug AgentSlug { get; }

    /// <summary>
    /// Gets the message source.
    /// </summary>
    public MessageSource From { get; }

    /// <summary>
    /// Gets the message recipient.
    /// </summary>
    public string To { get; }

    /// <summary>
    /// Gets the message timestamp.
    /// </summary>
    public DateTime? Date { get; }

    /// <summary>
    /// Gets the message priority.
    /// </summary>
    public Priority Priority { get; }

    /// <summary>
    /// Gets the message subject.
    /// </summary>
    public string Re { get; }

    /// <summary>
    /// Gets the message type.
    /// </summary>
    public MessageType Type { get; }

    /// <summary>
    /// Gets the message body.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// Gets a value indicating whether the message has been processed.
    /// </summary>
    public bool IsProcessed { get; }

    /// <summary>
    /// Gets the optional associated task identifier.
    /// </summary>
    public TaskId? TaskId { get; }

    /// <summary>
    /// Gets the optional associated playbook slug.
    /// </summary>
    public string? Playbook { get; }

    private static Priority NormalizePriority(Priority? priority)
        => priority ?? Priority.Normal;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
