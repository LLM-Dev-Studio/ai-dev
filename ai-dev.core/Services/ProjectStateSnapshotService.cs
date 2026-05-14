using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;

using Microsoft.Extensions.Logging;

namespace AiDev.Services;

/// <summary>
/// Computes a single project state snapshot so different UIs use the same source of truth.
/// </summary>
public partial class ProjectStateSnapshotService(
    MessagesService messagesService,
    IDecisionsService decisionsService,
    IBoardService boardService,
    AgentService agentService,
    AgentRunnerService agentRunnerService,
    ILogger<ProjectStateSnapshotService> logger)
{
    public ProjectStateSnapshot GetSnapshot(ProjectSlug projectSlug)
    {
        var unreadMessages = SafeCount(() => messagesService.ListMessages(projectSlug).Count(m => !m.IsProcessed), "unread-messages");
        var pendingDecisions = SafeCount(() => decisionsService.ListDecisions(projectSlug, DecisionStatus.Pending).Count, "pending-decisions");

        var board = SafeGet(() => boardService.LoadBoard(projectSlug), "board");
        var openBoardTasks = 0;
        if (board != null)
        {
            var doneIds = board.Columns.FirstOrDefault(c => c.Id == ColumnId.Done)?.TaskIds
                .ToHashSet() ?? [];
            openBoardTasks = board.Tasks.Keys.Count(taskId => !doneIds.Contains(taskId));
        }

        var agents = SafeGet(() => agentService.ListAgents(projectSlug), "agents") ?? [];
        var runningAgents = agents.Count(agent => agentRunnerService.IsRunning(projectSlug, agent.Slug));
        var agentsWithPendingInbox = agents.Count(agent => agent.InboxCount > 0);

        return new ProjectStateSnapshot(
            ProjectSlug: projectSlug,
            UnreadMessageCount: unreadMessages,
            PendingDecisionCount: pendingDecisions,
            OpenBoardTaskCount: openBoardTasks,
            RunningAgentCount: runningAgents,
            AgentsWithPendingInboxCount: agentsWithPendingInbox);
    }

    private int SafeCount(Func<int> countFactory, string context)
    {
        try
        {
            return countFactory();
        }
        catch (Exception ex)
        {
            LogSafeCountFailed(ex, context);
            return 0;
        }
    }

    private T? SafeGet<T>(Func<T> factory, string context)
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            LogSafeGetFailed(ex, context);
            return default;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[snapshot] SafeCount failed for {Context}")]
    private partial void LogSafeCountFailed(Exception ex, string context);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[snapshot] SafeGet failed for {Context}")]
    private partial void LogSafeGetFailed(Exception ex, string context);
}
