namespace AiDevNet.Models.Types;

public readonly record struct AgentStatus
{
    public static readonly AgentStatus Idle = new("idle");
    public static readonly AgentStatus Running = new("running");
    public static readonly AgentStatus Error = new("error");

    public string Value { get; }

    private AgentStatus(string value) => Value = value;

    public static AgentStatus From(string? value) => value?.ToLowerInvariant() switch
    {
        "running" => Running,
        "error" => Error,
        _ => Idle,
    };

    public bool IsIdle => this == Idle;
    public bool IsRunning => this == Running;
    public bool IsError => this == Error;

    public (string DotClass, string TextClass) BadgeClasses => Value switch
    {
        "running" => ("bg-emerald-400 animate-pulse", "text-emerald-400"),
        "error" => ("bg-red-400", "text-red-400"),
        _ => ("bg-zinc-500", "text-zinc-400"),
    };

    public override string ToString() => Value;
}
