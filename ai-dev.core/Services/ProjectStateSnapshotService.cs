using AiDev.Features.Agent;
using AiDev.Features.Board;
using AiDev.Features.Decision;

using Microsoft.Extensions.Logging;

namespace AiDev.Services;

/// <summary>
/// Computes a single project state snapshot so different UIs use the same source of truth.
/// </summary>
public class ProjectStateSnapshotService(
    MessagesService messagesService,
    DecisionsService decisionsService,
    BoardService boardService,
    AgentService agentService,
    AgentRunnerService agentRunnerService,
    ILogger<ProjectStateSnapshotService> logger)
{
    public ProjectStateSnapshot GetSnapshot(ProjectSlug projectSlug)
    {
        var unreadMessages = SafeCount(() => messagesService.ListMessages(projectSlug).Count(m => !m.IsProcessed), logger, "unread-messages");
        var pendingDecisions = SafeCount(() => decisionsService.ListDecisions(projectSlug, "pending").Count, logger, "pending-decisions");

        var board = SafeGet(() => boardService.LoadBoard(projectSlug), logger, "board");
        var openBoardTasks = 0;
        if (board != null)
        {
            var doneIds = board.Columns.FirstOrDefault(c => c.Id == ColumnId.Done)?.TaskIds
                .ToHashSet() ?? [];
            openBoardTasks = board.Tasks.Keys.Count(taskId => !doneIds.Contains(taskId));
        }

        var agents = SafeGet(() => agentService.ListAgents(projectSlug), logger, "agents") ?? [];
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

    private static int SafeCount(Func<int> countFactory, ILogger logger, string context)
    {
        try
        {
            return countFactory();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[snapshot] SafeCount failed for {Context}", context);
            return 0;
        }
    }

    private static T? SafeGet<T>(Func<T> factory, ILogger logger, string context)
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[snapshot] SafeGet failed for {Context}", context);
            return default;
        }
    }
}
