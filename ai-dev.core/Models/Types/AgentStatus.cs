namespace AiDev.Models.Types;

/// <summary>
/// Represents the current execution status of an agent.
/// </summary>
public readonly record struct AgentStatus
{
    /// <summary>
    /// Represents an idle agent.
    /// </summary>
    public static readonly AgentStatus Idle = new("idle");

    /// <summary>
    /// Represents a running agent.
    /// </summary>
    public static readonly AgentStatus Running = new("running");

    /// <summary>
    /// Represents an agent in an error state.
    /// </summary>
    public static readonly AgentStatus Error = new("error");

    /// <summary>
    /// Gets the persisted status value.
    /// </summary>
    public string Value { get; }

    private AgentStatus(string value) => Value = value;

    /// <summary>
    /// Creates an <see cref="AgentStatus"/> from a raw persisted value.
    /// </summary>
    /// <param name="value">The raw status value.</param>
    /// <returns>The parsed status, defaulting to <see cref="Idle"/>.</returns>
    public static AgentStatus From(string? value) => value?.ToLowerInvariant() switch
    {
        "running" => Running,
        "error" => Error,
        _ => Idle,
    };

    /// <summary>
    /// Gets a value indicating whether the status is idle.
    /// </summary>
    public bool IsIdle => this == Idle;

    /// <summary>
    /// Gets a value indicating whether the status is running.
    /// </summary>
    public bool IsRunning => this == Running;

    /// <summary>
    /// Gets a value indicating whether the status is error.
    /// </summary>
    public bool IsError => this == Error;

    /// <summary>
    /// Gets the display name for the status.
    /// </summary>
    public string DisplayName => Value switch
    {
        "running" => "Running",
        "error"   => "Error",
        _         => "Idle",
    };

    /// <summary>
    /// Gets the color associated with the status.
    /// </summary>
    public string ColorHex => Value switch
    {
        "running" => "#22C55E",
        "error"   => "#EF4444",
        _         => "#6B7280",
    };

    /// <summary>
    /// Gets the UI badge CSS classes for the status.
    /// </summary>
    public (string DotClass, string TextClass) BadgeClasses => Value switch
    {
        "running" => ("bg-emerald-400 animate-pulse", "text-emerald-400"),
        "error" => ("bg-red-400", "text-red-400"),
        _ => ("bg-zinc-500", "text-zinc-400"),
    };

    public override string ToString() => Value;
}
