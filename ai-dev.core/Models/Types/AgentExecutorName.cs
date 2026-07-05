namespace AiDev.Models.Types;

/// <summary>
/// A supported agent executor identifier persisted in agent.json.
/// </summary>
[JsonConverter(typeof(AgentExecutorNameJsonConverter))]
public sealed record AgentExecutorName
{
    /// <summary>
    /// The persisted value for Claude CLI.
    /// </summary>
    public const string ClaudeValue = "claude";

    /// <summary>
    /// The persisted value for the Anthropic API executor.
    /// </summary>
    public const string AnthropicValue = "anthropic";

    /// <summary>
    /// The persisted value for Ollama.
    /// </summary>
    public const string OllamaValue = "ollama";

    /// <summary>
    /// The persisted value for GitHub Models.
    /// </summary>
    public const string GitHubModelsValue = "github-models";

    /// <summary>
    /// The persisted value for LM Studio.
    /// </summary>
    public const string LmStudioValue = "lmstudio";

    /// <summary>
    /// The persisted value for Copilot CLI.
    /// </summary>
    public const string CopilotCliValue = "copilot-cli";

    private AgentExecutorName(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the persisted executor value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the user-facing executor display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the Claude CLI executor.
    /// </summary>
    public static AgentExecutorName Claude { get; } = new(ClaudeValue, "Claude CLI");

    /// <summary>
    /// Gets the Anthropic API executor.
    /// </summary>
    public static AgentExecutorName Anthropic { get; } = new(AnthropicValue, "Anthropic API");

    /// <summary>
    /// Gets the Ollama executor.
    /// </summary>
    public static AgentExecutorName Ollama { get; } = new(OllamaValue, "Ollama");

    /// <summary>
    /// Gets the GitHub Models executor.
    /// </summary>
    public static AgentExecutorName GitHubModels { get; } = new(GitHubModelsValue, "GitHub Models");

    /// <summary>
    /// Gets the LM Studio executor.
    /// </summary>
    public static AgentExecutorName LmStudio { get; } = new(LmStudioValue, "LM Studio");

    /// <summary>
    /// Gets the Copilot CLI executor.
    /// </summary>
    public static AgentExecutorName CopilotCli { get; } = new(CopilotCliValue, "Copilot CLI");

    /// <summary>
    /// Gets the default executor.
    /// </summary>
    public static AgentExecutorName Default => Claude;

    /// <summary>
    /// Gets the supported executor set.
    /// </summary>
    public static IReadOnlyList<AgentExecutorName> Supported { get; } = [Claude, Anthropic, Ollama, GitHubModels, LmStudio, CopilotCli];

    /// <summary>
    /// Attempts to parse a persisted executor value.
    /// </summary>
    /// <param name="value">The raw executor value.</param>
    /// <param name="executor">The parsed executor when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out AgentExecutorName? executor)
    {
        executor = value switch
        {
            ClaudeValue => Claude,
            AnthropicValue => Anthropic,
            OllamaValue => Ollama,
            GitHubModelsValue => GitHubModels,
            LmStudioValue => LmStudio,
            CopilotCliValue => CopilotCli,
            _ => null,
        };

        return executor is not null;
    }

    /// <summary>
    /// Converts the executor to its persisted string value.
    /// </summary>
    /// <param name="executor">The executor to convert.</param>
    public static implicit operator string(AgentExecutorName executor) => executor.Value;

    public override string ToString() => Value;
}
