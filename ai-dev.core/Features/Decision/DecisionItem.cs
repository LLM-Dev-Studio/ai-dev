namespace AiDev.Features.Decision;

/// <summary>
/// Represents a decision request and its eventual resolution state.
/// </summary>
public sealed class DecisionItem
{
    [JsonIgnore] private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>
    /// Creates a decision with normalized defaults and validated required fields.
    /// </summary>
    public DecisionItem(
        string filename,
        DecisionId id,
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
        ArgumentNullException.ThrowIfNull(id);
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

    /// <summary>
    /// Gets the backing filename for the decision.
    /// </summary>
    public string Filename { get; }

    /// <summary>
    /// Gets the unique decision identifier.
    /// </summary>
    public DecisionId Id { get; }

    /// <summary>
    /// Gets the decision source.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the decision creation timestamp.
    /// </summary>
    public DateTime? Date { get; }

    /// <summary>
    /// Gets the decision priority.
    /// </summary>
    public Priority Priority { get; private set; }

    /// <summary>
    /// Gets the decision subject.
    /// </summary>
    public string Subject { get; private set; }

    /// <summary>
    /// Gets the current decision status.
    /// </summary>
    public DecisionStatus Status { get; private set; }

    /// <summary>
    /// Gets the optional blocker reference.
    /// </summary>
    public string? Blocks { get; private set; }

    /// <summary>
    /// Gets the timestamp when the decision was resolved.
    /// </summary>
    public DateTime? ResolvedAt { get; private set; }

    /// <summary>
    /// Gets the actor who resolved the decision.
    /// </summary>
    public string? ResolvedBy { get; private set; }

    /// <summary>
    /// Gets the decision body content.
    /// </summary>
    public string Body { get; private set; }

    /// <summary>
    /// Gets the resolution response content.
    /// </summary>
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
