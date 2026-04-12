namespace AiDev.Models.Types;

/// <summary>
/// A supported agent executor identifier persisted in agent.json.
/// </summary>
[JsonConverter(typeof(AgentExecutorNameJsonConverter))]
public sealed record AgentExecutorName
{
    public const string ClaudeValue = "claude";
    public const string AnthropicValue = "anthropic";
    public const string OllamaValue = "ollama";
    public const string GitHubModelsValue = "github-models";
    public const string LmStudioValue = "lmstudio";

    private AgentExecutorName(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public string Value { get; }
    public string DisplayName { get; }

    public static AgentExecutorName Claude { get; } = new(ClaudeValue, "Claude CLI");
    public static AgentExecutorName Anthropic { get; } = new(AnthropicValue, "Anthropic API");
    public static AgentExecutorName Ollama { get; } = new(OllamaValue, "Ollama");
    public static AgentExecutorName GitHubModels { get; } = new(GitHubModelsValue, "GitHub Models");
    public static AgentExecutorName LmStudio { get; } = new(LmStudioValue, "LM Studio");

    public static AgentExecutorName Default => Claude;
    public static IReadOnlyList<AgentExecutorName> Supported { get; } = [Claude, Anthropic, Ollama, GitHubModels, LmStudio];

    public static bool TryParse(string? value, [NotNullWhen(true)] out AgentExecutorName? executor)
    {
        executor = value switch
        {
            ClaudeValue => Claude,
            AnthropicValue => Anthropic,
            OllamaValue => Ollama,
            GitHubModelsValue => GitHubModels,
            LmStudioValue => LmStudio,
            _ => null,
        };

        return executor is not null;
    }

    public static implicit operator string(AgentExecutorName executor) => executor.Value;

    public override string ToString() => Value;
}

internal sealed class AgentExecutorNameJsonConverter : JsonConverter<AgentExecutorName>
{
    public override AgentExecutorName? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return AgentExecutorName.TryParse(value, out var executor) ? executor : null;
    }

    public override void Write(Utf8JsonWriter writer, AgentExecutorName value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
