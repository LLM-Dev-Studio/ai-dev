namespace AiDev.Features.Agent;

internal sealed class TaskAssignedHandler(
    IAgentRunnerService agentRunner,
    ILogger<TaskAssignedHandler> logger) : IDomainEventHandler<TaskAssigned>
{
    public Task Handle(TaskAssigned domainEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var result = agentRunner.WriteInboxMessage(
            projectSlug: domainEvent.ProjectSlug,
            agentSlug: domainEvent.Assignee,
            from: "board",
            re: domainEvent.Title,
            type: "task-assigned",
            priority: domainEvent.Priority.Value,
            body: $"You have been assigned a new task: {domainEvent.Title}{(string.IsNullOrWhiteSpace(domainEvent.Description) ? string.Empty : $"\n\n{domainEvent.Description}")}",
            taskId: domainEvent.TaskId);

        if (result is Err<Unit> err)
        {
            logger.LogError("[board] Failed to dispatch TaskAssigned to {Assignee} for task {TaskId}: {Error}",
                domainEvent.Assignee, domainEvent.TaskId, err.Error.Message);
            throw new InvalidOperationException(
                $"Failed to dispatch task to agent '{domainEvent.Assignee}': {err.Error.Message}");
        }

        logger.LogInformation("[board] Dispatched TaskAssigned to {Assignee} for task {TaskId} ({Title})",
            domainEvent.Assignee, domainEvent.TaskId, domainEvent.Title);

        return Task.CompletedTask;
    }
}
