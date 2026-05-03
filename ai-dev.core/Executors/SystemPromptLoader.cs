using System.Text;

namespace AiDev.Executors;

/// <summary>
/// Selects and loads the appropriate system prompt tier (full or compact) for local model executors,
/// and builds the deterministic refusal message when even the compact prompt will not fit.
/// </summary>
public static class SystemPromptLoader
{
    public const string Fallback = "You are a helpful AI agent.";

    /// <summary>
    /// Reads CLAUDE.md (full) and CLAUDE.compact.md (compact) from the agent's working directory,
    /// then delegates tier selection to <see cref="TokenBudget.SelectSystemPrompt"/>.
    /// Falls back to <see cref="Fallback"/> when neither file exists.
    /// When no compact file exists, the full prompt is used regardless of context window size.
    /// </summary>
    public static string Load(string workingDir, int contextWindow, int threshold)
    {
        var fullPath    = Path.Combine(workingDir, "CLAUDE.md");
        var compactPath = Path.Combine(workingDir, "CLAUDE.compact.md");

        var full    = File.Exists(fullPath)    ? File.ReadAllText(fullPath, Encoding.UTF8)    : Fallback;
        var compact = File.Exists(compactPath) ? File.ReadAllText(compactPath, Encoding.UTF8) : full;

        return TokenBudget.SelectSystemPrompt(contextWindow, full, compact, threshold);
    }

    /// <summary>
    /// Builds a deterministic, human-readable refusal message when the model's context window
    /// is too small to run the agent. Generated entirely in code — no LLM involvement.
    /// </summary>
    public static string BuildRefusalMessage(string modelId, int contextWindow, int minRequired) =>
        $"[SESSION REFUSED] Model '{modelId}' has a context window of {contextWindow} tokens. " +
        $"This agent requires a minimum of {minRequired} tokens to run. " +
        $"Switch to a model with a larger context window.";
}
