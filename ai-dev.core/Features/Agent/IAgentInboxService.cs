namespace AiDev.Features.Agent;

/// <summary>
/// Writes inbox messages for agents and notifies project state changes.
/// </summary>
public interface IAgentInboxService
{
    /// <summary>
    /// Writes a message into an agent inbox.
    /// </summary>
    /// <param name="projectSlug">The project that owns the agent.</param>
    /// <param name="agentSlug">The target agent slug.</param>
    /// <param name="from">The message source.</param>
    /// <param name="re">The message subject.</param>
    /// <param name="type">The message type.</param>
    /// <param name="priority">The message priority.</param>
    /// <param name="body">The message body.</param>
    /// <param name="taskId">The optional associated task identifier.</param>
    /// <param name="decisionId">The optional associated decision identifier.</param>
    /// <returns>The result of the write operation.</returns>
    Result<Unit> WriteInboxMessage(ProjectSlug projectSlug, AgentSlug agentSlug,
        MessageSource from, string re, MessageType type, Priority priority, string body,
        TaskId? taskId = null, DecisionId? decisionId = null);
}
