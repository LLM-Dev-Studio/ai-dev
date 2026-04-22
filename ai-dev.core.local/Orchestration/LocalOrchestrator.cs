namespace AiDev.Core.Local.Orchestration;

internal sealed class LocalOrchestrator(
    ILocalPlanner planner,
    ILocalToolBroker toolBroker,
    IContextCompactor compactor,
    IRuntimeMemoryStore memoryStore,
    IModelStrategyResolver strategyResolver,
    LocalOrchestratorOptions options) : ILocalOrchestrator
{
    public async Task<Result<CompactionSnapshot>> RunAsync(
        LocalObjective objective,
        RuntimeModelProfile modelProfile,
        CancellationToken ct = default)
    {
        var strategyResult = strategyResolver.Resolve(modelProfile);
        if (strategyResult is not Ok<RuntimeModelStrategy> { Value: var strategy })
            return new Err<CompactionSnapshot>(((Err<RuntimeModelStrategy>)strategyResult).Error);

        var state = new LocalRuntimeState(
            Objective: objective,
            Transcript: new RuntimeTranscript([], []),
            Budget: options.DefaultBudget,
            ModelProfile: modelProfile,
            StartedAtUtc: DateTimeOffset.UtcNow,
            Iteration: 0);

        for (int i = 0; i < options.MaxIterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            // 1. Plan next action
            var planResult = await planner.PlanNextAsync(state, ct).ConfigureAwait(false);
            if (planResult is not Ok<RuntimeActionPlan> { Value: var plan })
                return new Err<CompactionSnapshot>(((Err<RuntimeActionPlan>)planResult).Error);

            // 2. Terminate successfully when planner signals done
            if (plan.ToolRequests.Count == 0 && !plan.RequiresUserInput)
                return await FinalCompactAsync(state, objective.CorrelationId, ct).ConfigureAwait(false);

            if (plan.RequiresUserInput)
                return new Err<CompactionSnapshot>(new DomainError(
                    "LocalOrchestrator.BlockedOnInput",
                    $"Orchestrator blocked waiting for user input: {plan.Intent}"));

            // 3. Execute tools
            var toolsResult = await toolBroker.ExecuteAsync(plan.ToolRequests, ct).ConfigureAwait(false);
            if (toolsResult is not Ok<IReadOnlyList<ToolOutcome>> { Value: var outcomes })
                return new Err<CompactionSnapshot>(((Err<IReadOnlyList<ToolOutcome>>)toolsResult).Error);

            // 4. Append observations
            var now = DateTimeOffset.UtcNow;
            var observations = outcomes
                .Select(o => new RuntimeObservation(now, o.ToolName, o.Summary, o.Evidence))
                .ToList();

            state = state with
            {
                Transcript = new RuntimeTranscript(
                    [.. state.Transcript.Observations, .. observations],
                    state.Transcript.Decisions),
                Iteration = i + 1,
            };

            // 5. Compact on interval
            if (state.Iteration % strategy.CompactionInterval == 0)
            {
                var snap = compactor.Compact(state);
                if (snap is Ok<CompactionSnapshot> { Value: var interim })
                    await memoryStore.SaveSnapshotAsync(objective.CorrelationId, interim, ct).ConfigureAwait(false);
            }

            // 6. Enforce tool-call budget
            if (state.Transcript.Observations.Count >= state.Budget.MaxToolCalls)
                return new Err<CompactionSnapshot>(new DomainError(
                    "LocalOrchestrator.BudgetExhausted",
                    $"Tool call budget of {state.Budget.MaxToolCalls} exhausted after {state.Iteration} iterations."));
        }

        return new Err<CompactionSnapshot>(new DomainError(
            "LocalOrchestrator.MaxIterationsReached",
            $"Objective did not complete within {options.MaxIterations} iterations."));
    }

    private async Task<Result<CompactionSnapshot>> FinalCompactAsync(
        LocalRuntimeState state, Guid objectiveId, CancellationToken ct)
    {
        var result = compactor.Compact(state);
        if (result is Ok<CompactionSnapshot> { Value: var snapshot })
            await memoryStore.SaveSnapshotAsync(objectiveId, snapshot, ct).ConfigureAwait(false);
        return result;
    }
}
