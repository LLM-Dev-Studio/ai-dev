namespace AiDev.Features.Agent;

internal sealed class TaskAssignedHandler(
    AgentRunnerService agentRunner,
    ILogger<TaskAssignedHandler> logger) : IDomainEventHandler<TaskAssigned>
{
    public Task Handle(TaskAssigned domainEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var error = agentRunner.WriteInboxMessage(
            projectSlug: domainEvent.ProjectSlug,
            agentSlug: domainEvent.Assignee,
            from: "board",
            re: domainEvent.Title,
            type: "task-assigned",
            priority: domainEvent.Priority.Value,
            body: $"You have been assigned a new task: {domainEvent.Title}{(string.IsNullOrWhiteSpace(domainEvent.Description) ? string.Empty : $"\n\n{domainEvent.Description}")}",
            taskId: domainEvent.TaskId);

        if (error != null)
        {
            logger.LogError("[board] Failed to dispatch TaskAssigned to {Assignee} for task {TaskId}: {Error}",
                domainEvent.Assignee, domainEvent.TaskId, error);
            throw new InvalidOperationException(
                $"Failed to dispatch task to agent '{domainEvent.Assignee}': {error}");
        }

        logger.LogInformation("[board] Dispatched TaskAssigned to {Assignee} for task {TaskId} ({Title})",
            domainEvent.Assignee, domainEvent.TaskId, domainEvent.Title);

        return Task.CompletedTask;
    }
}
