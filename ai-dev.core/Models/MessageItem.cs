namespace AiDev.Models;

public sealed class MessageItem
{
    /// <summary>
    /// Creates a message with validated required fields and normalized optional metadata.
    /// </summary>
    public MessageItem(
        string filename,
        AgentSlug agentSlug,
        string from,
        string to,
        string re,
        string type,
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
        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException("From is required.", nameof(from));
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("To is required.", nameof(to));
        if (string.IsNullOrWhiteSpace(re))
            throw new ArgumentException("Message subject is required.", nameof(re));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Message type is required.", nameof(type));
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

    public string Filename { get; }
    public AgentSlug AgentSlug { get; }
    public string From { get; }
    public string To { get; }
    public DateTime? Date { get; }
    public Priority Priority { get; }
    public string Re { get; }
    public string Type { get; }
    public string Body { get; }
    public bool IsProcessed { get; }
    public TaskId? TaskId { get; }
    public string? Playbook { get; }

    private static Priority NormalizePriority(Priority? priority)
        => priority ?? Priority.Normal;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
