namespace AiDev.Models;

public sealed record ProjectStateSnapshot(
    ProjectSlug ProjectSlug,
    int UnreadMessageCount,
    int PendingDecisionCount,
    int OpenBoardTaskCount,
    int RunningAgentCount,
    int AgentsWithPendingInboxCount);
