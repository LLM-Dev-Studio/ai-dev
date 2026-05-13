namespace AiDev.Models;

/// <summary>
/// Represents a summarized snapshot of project activity state.
/// </summary>
/// <param name="ProjectSlug">The project represented by the snapshot.</param>
/// <param name="UnreadMessageCount">The number of unread messages.</param>
/// <param name="PendingDecisionCount">The number of pending decisions.</param>
/// <param name="OpenBoardTaskCount">The number of open board tasks.</param>
/// <param name="RunningAgentCount">The number of currently running agents.</param>
/// <param name="AgentsWithPendingInboxCount">The number of agents with pending inbox items.</param>
public sealed record ProjectStateSnapshot(
    ProjectSlug ProjectSlug,
    int UnreadMessageCount,
    int PendingDecisionCount,
    int OpenBoardTaskCount,
    int RunningAgentCount,
    int AgentsWithPendingInboxCount);
