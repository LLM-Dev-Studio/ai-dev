namespace AiDev.Executors;

/// <summary>
/// Lightweight token-budget helper for local executors (LM Studio, Ollama).
/// Uses a conservative characters-per-token estimator to preflight requests
/// against a model's loaded context window — we only need to decide whether
/// to send a request, not count tokens exactly.
/// </summary>
public static class TokenBudget
{
    /// <summary>Characters-per-token approximation used by the estimator.</summary>
    public const int CharsPerToken = 4;

    /// <summary>Per-message framing overhead we add to each message (role tag, separators).</summary>
    public const int PerMessageOverhead = 8;

    /// <summary>Safety margin added on top of the estimated request size.</summary>
    public const int SafetyMargin = 64;

    /// <summary>
    /// Estimate the token count of a single string using a chars-per-token ratio.
    /// Conservative (rounds up). Returns 0 for null or empty.
    /// </summary>
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (text.Length + CharsPerToken - 1) / CharsPerToken;
    }

    /// <summary>
    /// Estimate the total token count for a sequence of message contents,
    /// including per-message framing overhead.
    /// </summary>
    public static int EstimateMessagesTokens(IEnumerable<string?> contents)
    {
        int total = 0;
        int count = 0;
        foreach (var c in contents)
        {
            total += EstimateTokens(c);
            count++;
        }
        return total + (count * PerMessageOverhead);
    }

    /// <summary>
    /// Check whether a chat request is expected to fit the model's loaded context window.
    ///
    /// When <paramref name="contextWindow"/> is 0 (unknown), this returns Fits=true
    /// and skips the check — callers should still send the request and surface any
    /// provider-side error that comes back.
    /// </summary>
    public static PreflightResult Preflight(
        int contextWindow,
        int maxOutputTokens,
        IEnumerable<string?> messageContents,
        string? toolsJson,
        string modelId,
        string executorName)
    {
        var messageTokens = EstimateMessagesTokens(messageContents);
        var toolTokens    = EstimateTokens(toolsJson);
        var required      = messageTokens + toolTokens + maxOutputTokens + SafetyMargin;

        if (contextWindow <= 0 || required <= contextWindow)
            return new PreflightResult.Fits(required);

        var suggestion = SuggestContextWindow(required);
        var error =
            $"Model '{modelId}' ({executorName}) has context_length={contextWindow}, "
          + $"but this request needs approximately {required} tokens "
          + $"(messages={messageTokens}, tools={toolTokens}, output_reservation={maxOutputTokens}, margin={SafetyMargin}). "
          + $"Reload the model in {executorName} with a larger context window "
          + $"(recommended: {suggestion}) or reduce the agent's CLAUDE.md / disable workspace tools.";

        return new PreflightResult.Exceeded(required, error);
    }

    /// <summary>
    /// Suggest the next power-of-two context window that gives the request ~25% headroom.
    /// Clamped to the range [2048, 131072].
    /// </summary>
    public static int SuggestContextWindow(int requiredTokens)
    {
        var target = (int)Math.Min(int.MaxValue, Math.Ceiling(requiredTokens * 1.25));
        var pow    = 2048;
        while (pow < target && pow < 131072) pow <<= 1;
        return pow;
    }

    /// <summary>
    /// Compute a reasonable output-token reservation given the model's loaded context window.
    /// Aims for ~25% of the window, clamped to [<paramref name="floor"/>, <paramref name="ceiling"/>].
    /// When the context window is unknown (0), returns <paramref name="ceiling"/>.
    /// </summary>
    public static int RecommendMaxOutputTokens(int contextWindow, int floor = 512, int ceiling = 4096)
    {
        if (contextWindow <= 0) return ceiling;
        var quarter = contextWindow / 4;
        return Math.Clamp(quarter, floor, ceiling);
    }

    /// <summary>
    /// Returns the maximum token budget for the system prompt: half the context window.
    /// Returns 0 when the context window is unknown — callers should skip truncation in that case.
    /// </summary>
    public static int MaxSystemPromptTokens(int contextWindow) =>
        contextWindow > 0 ? contextWindow / 2 : 0;

    /// <summary>
    /// Truncate <paramref name="text"/> so it fits within <paramref name="maxTokens"/> using
    /// the same character-based estimator as the rest of the budget system. Returns the original
    /// string unchanged when it already fits.
    /// </summary>
    public static string TruncateSystemPrompt(string text, int maxTokens)
    {
        var maxChars = maxTokens * CharsPerToken;
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    /// <summary>
    /// Select the appropriate system prompt tier based on the model's context window and the
    /// configured threshold. Returns <paramref name="compactPrompt"/> when the context window
    /// is known and below <paramref name="threshold"/>; otherwise returns <paramref name="fullPrompt"/>.
    /// </summary>
    public static string SelectSystemPrompt(
        int contextWindow, string fullPrompt, string compactPrompt, int threshold) =>
        contextWindow > 0 && contextWindow < threshold ? compactPrompt : fullPrompt;

    /// <summary>
    /// Check whether the compact system prompt and the output reservation fit within the
    /// model's context window. Returns true when the context window is unknown (0).
    /// </summary>
    public static bool CanFitCompact(int contextWindow, string compactPrompt, int maxOutputTokens)
    {
        if (contextWindow <= 0) return true;
        var required = EstimateTokens(compactPrompt) + maxOutputTokens + SafetyMargin;
        return required <= contextWindow;
    }
}
