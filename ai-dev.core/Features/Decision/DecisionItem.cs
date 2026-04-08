namespace AiDev.Features.Decision;

public sealed class DecisionItem
{
    [JsonIgnore] private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>
    /// Creates a decision with normalized defaults and validated required fields.
    /// </summary>
    public DecisionItem(
        string filename,
        string id,
        string from,
        string subject,
        string body,
        DateTime? date = null,
        Priority? priority = null,
        DecisionStatus? status = null,
        string? blocks = null,
        DateTime? resolvedAt = null,
        string? resolvedBy = null,
        string? response = null)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename is required.", nameof(filename));
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Decision id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException("Decision source is required.", nameof(from));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Decision subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Decision body is required.", nameof(body));

        Filename = filename;
        Id = id;
        From = from;
        Date = date;
        Priority = NormalizePriority(priority);
        Subject = subject;
        Status = NormalizeStatus(status);
        Blocks = NormalizeOptional(blocks);
        ResolvedAt = resolvedAt;
        ResolvedBy = NormalizeOptional(resolvedBy);
        Body = body.Trim();
        Response = NormalizeOptional(response);
    }

    public string Filename { get; }
    public string Id { get; }
    public string From { get; }
    public DateTime? Date { get; }
    public Priority Priority { get; private set; }
    public string Subject { get; private set; }
    public DecisionStatus Status { get; private set; }
    public string? Blocks { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }
    public string Body { get; private set; }
    public string? Response { get; private set; }

    /// <summary>
    /// Marks a pending decision as resolved by a named actor with a required response.
    /// </summary>
    public void Resolve(string resolvedBy, string response, DateTime resolvedAt)
    {
        if (!Status.IsPending)
            throw new InvalidOperationException("Decision is already resolved.");
        if (string.IsNullOrWhiteSpace(resolvedBy))
            throw new ArgumentException("Resolved by is required.", nameof(resolvedBy));
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Decision response is required.", nameof(response));

        Status = DecisionStatus.Resolved;
        ResolvedAt = resolvedAt;
        ResolvedBy = resolvedBy;
        Response = response.Trim();
        _domainEvents.Add(new DecisionResolved(Id, ResolvedBy, resolvedAt));
    }

    /// <summary>
    /// Drains pending domain events raised by decision transitions.
    /// </summary>
    public IReadOnlyList<DomainEvent> DequeueDomainEvents()
    {
        if (_domainEvents.Count == 0)
            return [];

        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }

    private static Priority NormalizePriority(Priority? priority)
        => priority ?? Priority.Normal;

    private static DecisionStatus NormalizeStatus(DecisionStatus? status)
        => status ?? DecisionStatus.Pending;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
