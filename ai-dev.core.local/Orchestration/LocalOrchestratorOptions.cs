namespace AiDev.Core.Local.Orchestration;

public sealed record LocalOrchestratorOptions(
    int MaxIterations,
    RuntimeBudget DefaultBudget,
    int MaxParallelTools = 1)
{
    public static LocalOrchestratorOptions Default { get; } = new(
        MaxIterations: 20,
        DefaultBudget: new RuntimeBudget(
            MaxToolCalls: 50,
            MaxExpandedFiles: 10,
            MaxRetriesPerError: 3,
            MaxContextTokens: 32_000),
        MaxParallelTools: 1);
}
