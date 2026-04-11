using AiDev.Executors;

namespace AiDev.Features.Agent;

public sealed class AgentInfo
{
    public AgentInfo(
        AgentSlug slug,
        string name,
        string role,
        string description,
        string? model = null,
        AgentStatus? status = null,
        DateTime? lastRunAt = null,
        int inboxCount = 0,
        AgentExecutorName? executor = null,
        IReadOnlyList<string>? skills = null,
        string? lastError = null,
        DateTime? lastErrorAt = null,
        ThinkingLevel thinkingLevel = ThinkingLevel.Off,
        AgentExecutorName? failoverExecutor = null,
        DateTime? failedOverAt = null)
    {
        ArgumentNullException.ThrowIfNull(slug);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name is required.", nameof(name));
        if (inboxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(inboxCount));

        Slug = slug;
        Name = name;
        Role = role ?? string.Empty;
        Description = description ?? string.Empty;
        Model = string.IsNullOrWhiteSpace(model) ? "sonnet" : model;
        Status = status ?? AgentStatus.Idle;
        LastRunAt = lastRunAt;
        InboxCount = inboxCount;
        Executor = executor ?? AgentExecutorName.Default;
        Skills = skills is null ? [] : [.. skills];
        LastError = string.IsNullOrWhiteSpace(lastError) ? null : lastError;
        LastErrorAt = lastErrorAt;
        ThinkingLevel = thinkingLevel;
        FailoverExecutor = failoverExecutor;
        FailedOverAt = failoverExecutor == null ? null : failedOverAt;
    }

    public AgentSlug Slug { get; }
    public string Name { get; private set; }
    public string Role { get; private set; }
    public string Model { get; private set; }
    public AgentStatus Status { get; private set; }
    public string Description { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public int InboxCount { get; private set; }
    public AgentExecutorName Executor { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }
    public ThinkingLevel ThinkingLevel { get; private set; }

    /// <summary>
    /// Skill keys enabled for this agent (e.g. ["git-read", "git-write"]).
    /// Empty means the executor uses its own defaults (preserves behaviour for existing agents).
    /// </summary>
    public IReadOnlyList<string> Skills { get; private set; }

    /// <summary>
    /// The executor this agent was automatically failed over to, or null if no failover has occurred.
    /// Set by AgentRunnerService when it detects an executor failure and switches the agent.
    /// </summary>
    public AgentExecutorName? FailoverExecutor { get; private set; }

    /// <summary>
    /// When the automatic failover occurred, or null if no failover has occurred.
    /// </summary>
    public DateTime? FailedOverAt { get; private set; }

    /// <summary>
    /// Updates editable agent metadata while keeping defaults and null handling consistent.
    /// </summary>
    public void UpdateMetadata(string name, string role, string description, string? model, AgentExecutorName? executor, IReadOnlyList<string>? skills, ThinkingLevel thinkingLevel = ThinkingLevel.Off)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name is required.", nameof(name));

        Name = name;
        Role = role ?? string.Empty;
        Description = description ?? string.Empty;
        Model = string.IsNullOrWhiteSpace(model) ? "sonnet" : model;
        Executor = executor ?? AgentExecutorName.Default;
        Skills = skills is null ? [] : [.. skills];
        ThinkingLevel = thinkingLevel;
    }

    /// <summary>
    /// Marks the agent as actively running and records when that run started.
    /// </summary>
    public void MarkRunning(DateTime startedAt)
    {
        Status = AgentStatus.Running;
        LastRunAt = startedAt;
    }

    /// <summary>
    /// Marks the agent as idle after work completes or when loading idle state.
    /// </summary>
    public void MarkIdle() => Status = AgentStatus.Idle;

    /// <summary>
    /// Marks the agent as faulted without exposing raw status mutation to callers.
    /// </summary>
    public void MarkError() => Status = AgentStatus.Error;

    /// <summary>
    /// Records the last session failure so the UI can guide the user.
    /// </summary>
    public void SetLastError(string? error, DateTime? occurredAt)
    {
        LastError = string.IsNullOrWhiteSpace(error) ? null : error;
        LastErrorAt = LastError == null ? null : occurredAt;
    }

    /// <summary>
    /// Synchronizes inbox count with the current workspace state.
    /// </summary>
    public void SetInboxCount(int inboxCount)
    {
        if (inboxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(inboxCount));

        InboxCount = inboxCount;
    }

    /// <summary>
    /// Records that this agent was automatically failed over to a fallback executor.
    /// </summary>
    public void RecordFailover(AgentExecutorName executor, DateTime at)
    {
        FailoverExecutor = executor;
        FailedOverAt = at;
    }

    /// <summary>
    /// Clears any recorded failover state (e.g. after the user manually reassigns the executor).
    /// </summary>
    public void ClearFailover()
    {
        FailoverExecutor = null;
        FailedOverAt = null;
    }
}
