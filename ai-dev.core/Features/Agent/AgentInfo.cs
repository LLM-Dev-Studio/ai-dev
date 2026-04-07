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
        string? executor = null,
        IReadOnlyList<string>? skills = null)
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
        Executor = string.IsNullOrWhiteSpace(executor) ? IAgentExecutor.Default : executor;
        Skills = skills is null ? [] : [.. skills];
    }

    public AgentSlug Slug { get; }
    public string Name { get; private set; }
    public string Role { get; private set; }
    public string Model { get; private set; }
    public AgentStatus Status { get; private set; }
    public string Description { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public int InboxCount { get; private set; }
    public string Executor { get; private set; }

    /// <summary>
    /// Skill keys enabled for this agent (e.g. ["git-read", "git-write"]).
    /// Empty means the executor uses its own defaults (preserves behaviour for existing agents).
    /// </summary>
    public IReadOnlyList<string> Skills { get; private set; }

    /// <summary>
    /// Updates editable agent metadata while keeping defaults and null handling consistent.
    /// </summary>
    public void UpdateMetadata(string name, string role, string description, string? model, string? executor, IReadOnlyList<string>? skills)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name is required.", nameof(name));

        Name = name;
        Role = role ?? string.Empty;
        Description = description ?? string.Empty;
        Model = string.IsNullOrWhiteSpace(model) ? "sonnet" : model;
        Executor = string.IsNullOrWhiteSpace(executor) ? IAgentExecutor.Default : executor;
        Skills = skills is null ? [] : [.. skills];
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
    /// Synchronizes inbox count with the current workspace state.
    /// </summary>
    public void SetInboxCount(int inboxCount)
    {
        if (inboxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(inboxCount));

        InboxCount = inboxCount;
    }
}
