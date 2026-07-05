namespace AiDev.Features.Agent;

internal sealed partial class TaskAssignedHandler(
    IAgentInboxService inbox,
    IAgentRunnerService runner,
    ILogger<TaskAssignedHandler> logger) : IDomainEventHandler<TaskAssigned>
{
    public Task Handle(TaskAssigned domainEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var result = inbox.WriteInboxMessage(
            projectSlug: domainEvent.ProjectSlug,
            agentSlug: domainEvent.Assignee,
            from: "board",
            re: domainEvent.Title,
            type: "task-assigned",
            priority: domainEvent.Priority,
            body: $"You have been assigned a new task: {domainEvent.Title}{(string.IsNullOrWhiteSpace(domainEvent.Description) ? string.Empty : $"\n\n{domainEvent.Description}")}",
            taskId: domainEvent.TaskId);

        if (result is Err<Unit> err)
        {
            // Log but do not throw — the agent is still launched below so it can discover
            // the task via the board. Throwing here would surface to the dispatcher and
            // swallow the event silently, leaving the task orphaned until OverwatchService.
            LogInboxWriteFailed(domainEvent.Assignee, domainEvent.TaskId, err.Error.Message);
        }
        else
        {
            LogTaskAssignedDispatched(domainEvent.Assignee, domainEvent.TaskId, domainEvent.Title);
        }

        // Always launch — the DispatcherService FSW will also fire if the inbox write
        // succeeded, but LaunchAgent is idempotent so the double-call is safe.
        // If the write failed the agent still runs and sees its assigned task in board.json.
        runner.LaunchAgent(
            domainEvent.ProjectSlug,
            domainEvent.Assignee,
            new AgentLaunchTrigger(
                Source: "task-assigned",
                Reason: "task assigned via board",
                ProjectSlug: domainEvent.ProjectSlug,
                TaskId: domainEvent.TaskId));

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "[board] Failed to write inbox message for {Assignee} task {TaskId}: {Error}")]
    private partial void LogInboxWriteFailed(AgentSlug assignee, TaskId taskId, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "[board] Dispatched TaskAssigned to {Assignee} for task {TaskId} ({Title})")]
    private partial void LogTaskAssignedDispatched(AgentSlug assignee, TaskId taskId, string title);
}
