namespace AiDev.Executors;

/// <summary>
/// Result of a preflight budget check. Either <see cref="Fits"/> (request is within budget)
/// or <see cref="Exceeded"/> (request is too large, with an error message).
/// </summary>
public abstract record PreflightResult(int Required)
{
    /// <summary>The request is expected to fit the context window (or the window is unknown).</summary>
    public sealed record Fits(int Required) : PreflightResult(Required);

    /// <summary>The request exceeds the context window. <see cref="Error"/> is a human-readable message.</summary>
    public sealed record Exceeded(int Required, string Error) : PreflightResult(Required);
}
