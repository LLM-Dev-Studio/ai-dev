using AiDev.Core.Local.Contracts;

namespace AiDev.Core.Local.Implementation.Null;

internal sealed class NullPlanner : ILocalPlanner
{
    public Task<Result<RuntimeActionPlan>> PlanNextAsync(LocalRuntimeState state, CancellationToken ct = default)
        => Task.FromResult<Result<RuntimeActionPlan>>(new Ok<RuntimeActionPlan>(
            new RuntimeActionPlan(
                Intent: "no-op",
                ToolRequests: [],
                ExpectedOutcome: "no-op",
                RequiresUserInput: false)));
}
