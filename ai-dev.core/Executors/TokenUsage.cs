namespace AiDev.Executors;

/// <summary>
/// Token consumption recorded at the end of an agent session.
/// Returned from <see cref="IAgentExecutor.RunAsync"/> so AgentRunnerService
/// can persist the data and the UI can display cost estimates.
/// </summary>
public sealed record TokenUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens = 0,
    long CacheWriteTokens = 0)
{
    public long TotalTokens => InputTokens + OutputTokens + CacheReadTokens + CacheWriteTokens;

    /// <summary>
    /// Returns estimated cost in USD for this session, or null when the model has no pricing data.
    /// </summary>
    public decimal? EstimatedCost(ModelDescriptor? model)
    {
        if (model == null || model.InputCostPer1MTokens == null || model.OutputCostPer1MTokens == null)
            return null;

        var inputCost  = InputTokens  * model.InputCostPer1MTokens.Value  / 1_000_000m;
        var outputCost = OutputTokens * model.OutputCostPer1MTokens.Value / 1_000_000m;
        // Cache reads are ~10% of input price (Anthropic pricing); cache writes are ~125% of input price.
        // For simplicity and cross-provider compatibility, we include only input+output in the estimate.
        return inputCost + outputCost;
    }

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.InputTokens  + b.InputTokens,
            a.OutputTokens + b.OutputTokens,
            a.CacheReadTokens  + b.CacheReadTokens,
            a.CacheWriteTokens + b.CacheWriteTokens);

    public static readonly TokenUsage Zero = new(0, 0);
}
